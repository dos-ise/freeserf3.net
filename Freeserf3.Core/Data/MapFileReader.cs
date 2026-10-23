/*
 * MapFileReader.cs - Reader for the Settlers 3 .map files
 *
 * Ported from settlers-remake (MIT) OriginalMapFileContentReader.java.
 *
 * Map files are big-endian. Layout:
 *   bytes 0-3:  rolling checksum
 *   bytes 4-7:  version (0x0A = original, 0x0B = Amazon)
 *   byte 8+:    sequence of parts { partType(int), partLen(int), data }
 * Each part is XOR-obfuscated with a rolling stream key.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.Text;

namespace Freeserf3.Data;

public enum MapFilePartType
{
    Eof = 0,
    MapInfo = 1,
    PlayerInfo = 2,
    TeamInfo = 3,
    Preview = 4,
    Unknown5 = 5,
    Area = 6,
    Settlers = 7,
    Buildings = 8,
    Stacks = 9,
    WinCond = 10,
    QuestText = 11,
    QuestTip = 12
}

/// <summary>One cell of the map area grid.</summary>
public struct MapNode
{
    public byte Height;
    public byte Landscape;
    public byte MapObject;
    public byte Player;
    public byte Accessible;
    public byte ResourceType1; // high nibble
    public byte ResourceType2; // low nibble
}

/// <summary>A building placed on the map.</summary>
public struct MapBuilding
{
    public byte Party;
    public byte BuildingType;
    public ushort PosX;
    public ushort PosY;
    public byte CountSword1, CountSword2, CountSword3;
    public byte CountArcher1, CountArcher2, CountArcher3;
    public byte CountSpear1, CountSpear2, CountSpear3;
}

/// <summary>A settler placed on the map.</summary>
public struct MapSettler
{
    public byte Party;
    public byte SettlerType;
    public ushort PosX;
    public ushort PosY;
}

/// <summary>A resource stack placed on the map.</summary>
public struct MapStack
{
    public ushort PosX;
    public ushort PosY;
    public byte StackType;
    public byte Count;
}

/// <summary>Player start information.</summary>
public sealed class MapPlayerInfo
{
    public int Nation;
    public int StartX;
    public int StartY;
    public string Name = "";
}

/// <summary>
/// Reads a Settlers 3 .map file: checksum, version, encrypted parts and the
/// area grid.
/// </summary>
public sealed class MapFileReader
{
    sealed class MapResourceInfo
    {
        public MapFilePartType PartType;
        public int Offset;
        public int Size;
        public int LastCryptKey;
        public readonly byte InitialCryptKey;
        int decryptLimit;

        public MapResourceInfo(MapFilePartType partType, int offset, int size, int cryptKey)
        {
            PartType = partType;
            Offset = offset;
            Size = size;
            InitialCryptKey = (byte)cryptKey;
            LastCryptKey = cryptKey;
        }

        public void Decrypt(byte[] mapContent, int targetDecryptLimit)
        {
            if (targetDecryptLimit > Size)
                throw new InvalidDataException("Map segment is shorter than targeted limit!");

            if (targetDecryptLimit <= decryptLimit)
                return;

            int pos = Offset + decryptLimit;
            if (pos + Size >= mapContent.Length)
                throw new InvalidDataException("Unable to decrypt map file: out of data!");

            int key = LastCryptKey;
            for (int i = targetDecryptLimit - decryptLimit; i > 0; i--)
            {
                int byt = mapContent[pos] ^ key;
                key = (key << 1) ^ byt;
                mapContent[pos] = (byte)byt;
                pos++;
            }

            LastCryptKey = key;
            decryptLimit = targetDecryptLimit;
        }

        public void ResetDecryptedFlag()
        {
            decryptLimit = 0;
            LastCryptKey = InitialCryptKey;
        }
    }

    readonly byte[] mapContent;
    readonly Dictionary<MapFilePartType, MapResourceInfo> resources = new();

    public int FileChecksum { get; private set; }
    public int WidthHeight { get; private set; }
    public bool SinglePlayerMap { get; private set; }
    public int PlayerCount { get; private set; }
    public int StartResources { get; private set; }
    public string MapQuestText { get; private set; } = "";
    public string MapQuestTip { get; private set; } = "";

    public MapNode[]? Area { get; private set; }
    public List<MapBuilding> Buildings { get; } = new();
    public List<MapSettler> Settlers { get; } = new();
    public List<MapStack> Stacks { get; } = new();
    public List<MapPlayerInfo> Players { get; } = new();

    public MapFileReader(byte[] mapContent)
    {
        this.mapContent = mapContent;
    }

    /// <summary>Verifies the rolling checksum stored in the file header.</summary>
    public bool IsChecksumValid()
    {
        int fileChecksum = ReadBeInt(0);
        FileChecksum = fileChecksum;

        int count = mapContent.Length & ~3;
        int currentChecksum = 0;

        for (int i = 8; i < count; i += 4)
        {
            int currentInt = ReadBeInt(i);
            currentChecksum = (int)(((uint)currentChecksum >> 31) | ((currentChecksum << 1) ^ currentInt));
        }

        return currentChecksum == fileChecksum;
    }

    /// <summary>Reads the part table (offsets and sizes of all parts).</summary>
    public void LoadMapResources()
    {
        int fileVersion = ReadBeInt(4);
        if (fileVersion != 0x0A && fileVersion != 0x0B)
            throw new InvalidDataException($"The version {fileVersion} is unknown");

        int dataLength = mapContent.Length;
        int filePos = 8;
        int partTypeTemp;

        do
        {
            partTypeTemp = ReadBeInt(filePos);
            int partLen = ReadBeInt(filePos + 4);

            int partType = partTypeTemp & 0x0000FFFF;
            int mapPartPos = filePos + 8;
            filePos += partLen;

            var type = (MapFilePartType)partType;
            if (type != MapFilePartType.Eof && partLen >= 0)
            {
                resources[type] = new MapResourceInfo(type, mapPartPos, partLen - 8, partType);
            }
        } while (partTypeTemp != 0 && filePos + 8 <= dataLength);
    }

    /// <summary>Reads the basic map information (info, players, quest texts, area size).</summary>
    public void ReadBasicMapInformation()
    {
        if (mapContent.Length < 100)
            return;

        FileChecksum = ReadBeInt(0);
        ReadMapInfo();
        ReadPlayerInfo();
        MapQuestText = ReadQuestText();
        MapQuestTip = ReadQuestTip();

        var areaPart = FindResource(MapFilePartType.Area);
        if (areaPart == null || areaPart.Size < 4)
            return;

        areaPart.Decrypt(mapContent, 4);
        WidthHeight = ReadBeInt(areaPart.Offset);
    }

    /// <summary>Reads the full area grid (terrain data).</summary>
    public void ReadMapData()
    {
        var filePart = FindAndDecryptFilePartSafe(MapFilePartType.Area);
        int pos = filePart.Offset;

        int widthHeight = ReadBeInt(pos);
        pos += 4;

        int dataCount = widthHeight * widthHeight;
        var area = new MapNode[dataCount];

        for (int i = 0; i < dataCount; i++)
        {
            area[i].Height = mapContent[pos++];
            area[i].Landscape = mapContent[pos++];
            area[i].MapObject = mapContent[pos++];
            area[i].Player = mapContent[pos++];
            area[i].Accessible = mapContent[pos++];
            area[i].ResourceType1 = (byte)((mapContent[pos] >> 4) & 0x0F);
            area[i].ResourceType2 = (byte)(mapContent[pos] & 0x0F);
            pos++;
        }

        Area = area;
    }

    /// <summary>Reads the buildings placed on the map.</summary>
    public void ReadBuildings()
    {
        var filePart = FindAndDecryptFilePart(MapFilePartType.Buildings);
        if (filePart == null)
            return;

        int pos = filePart.Offset;
        int buildingsCount = ReadBeInt(pos);
        pos += 4;

        if (buildingsCount * 12 > filePart.Size || buildingsCount < 0)
            throw new InvalidDataException($"wrong number of buildings in map file: {buildingsCount}");

        for (int i = 0; i < buildingsCount; i++)
        {
            var b = new MapBuilding
            {
                Party = mapContent[pos++],
                BuildingType = mapContent[pos++],
                PosX = (ushort)ReadBeWord(pos),
                PosY = (ushort)ReadBeWord(pos + 2)
            };
            pos += 4;
            pos++; // filling byte

            b.CountSword1 = (byte)((mapContent[pos] >> 4) & 0x0F);
            b.CountSword2 = (byte)(mapContent[pos] & 0x0F);
            pos++;

            b.CountArcher2 = (byte)((mapContent[pos] >> 4) & 0x0F);
            b.CountArcher3 = (byte)(mapContent[pos] & 0x0F);
            pos++;

            b.CountSword3 = (byte)((mapContent[pos] >> 4) & 0x0F);
            b.CountArcher1 = (byte)(mapContent[pos] & 0x0F);
            pos++;

            pos++; // high nibble = spear3, low nibble unused

            b.CountSpear1 = (byte)((mapContent[pos] >> 4) & 0x0F);
            b.CountSpear2 = (byte)(mapContent[pos] & 0x0F);
            pos++;

            Buildings.Add(b);
        }
    }

    /// <summary>Reads the settlers placed on the map.</summary>
    public void ReadSettlers()
    {
        var filePart = FindAndDecryptFilePart(MapFilePartType.Settlers);
        if (filePart == null)
            return;

        int pos = filePart.Offset;
        int settlerCount = ReadBeInt(pos);
        pos += 4;

        if (settlerCount * 6 > filePart.Size || settlerCount < 0)
            throw new InvalidDataException($"wrong number of settlers in map file: {settlerCount}");

        for (int i = 0; i < settlerCount; i++)
        {
            var s = new MapSettler
            {
                Party = mapContent[pos++],
                SettlerType = mapContent[pos++],
                PosX = (ushort)ReadBeWord(pos),
                PosY = (ushort)ReadBeWord(pos + 2)
            };
            pos += 4;
            Settlers.Add(s);
        }
    }

    /// <summary>Reads the resource stacks placed on the map.</summary>
    public void ReadStacks()
    {
        var filePart = FindAndDecryptFilePart(MapFilePartType.Stacks);
        if (filePart == null)
            return;

        int pos = filePart.Offset;
        int stackCount = ReadBeInt(pos);
        pos += 4;

        if (stackCount * 8 > filePart.Size || stackCount < 0)
            throw new InvalidDataException($"wrong number of stacks in map file: {stackCount}");

        for (int i = 0; i < stackCount; i++)
        {
            var s = new MapStack
            {
                PosX = (ushort)ReadBeWord(pos),
                PosY = (ushort)ReadBeWord(pos + 2)
            };
            pos += 4;
            s.StackType = mapContent[pos++];
            s.Count = mapContent[pos++];
            pos += 2; // padding
            Stacks.Add(s);
        }
    }

    void ReadMapInfo()
    {
        var filePart = FindAndDecryptFilePartSafe(MapFilePartType.MapInfo);
        int pos = filePart.Offset;

        int mapType = ReadBeInt(pos);
        pos += 4;

        if (mapType == 1)
            SinglePlayerMap = true;
        else if (mapType == 0)
            SinglePlayerMap = false;
        else
            throw new InvalidDataException($"wrong value for 'singlePlayerMap' {mapType} in mapfile!");

        PlayerCount = ReadBeInt(pos);
        pos += 4;
        StartResources = ReadBeInt(pos);
    }

    void ReadPlayerInfo()
    {
        var filePart = FindAndDecryptFilePartSafe(MapFilePartType.PlayerInfo);
        int pos = filePart.Offset;

        for (int i = 0; i < PlayerCount; i++)
        {
            var player = new MapPlayerInfo
            {
                Nation = ReadBeInt(pos),
                StartX = ReadBeInt(pos + 4),
                StartY = ReadBeInt(pos + 8),
                Name = ReadCStr(pos + 12, 33)
            };
            pos += 12 + 33;
            Players.Add(player);
        }
    }

    string ReadQuestText()
    {
        var filePart = FindAndDecryptFilePart(MapFilePartType.QuestText);
        return filePart == null ? "" : ReadCStr(filePart.Offset, filePart.Size);
    }

    string ReadQuestTip()
    {
        var filePart = FindAndDecryptFilePart(MapFilePartType.QuestTip);
        return filePart == null ? "" : ReadCStr(filePart.Offset, filePart.Size);
    }

    MapResourceInfo? FindResource(MapFilePartType type)
    {
        return resources.TryGetValue(type, out var r) ? r : null;
    }

    MapResourceInfo? FindAndDecryptFilePart(MapFilePartType partType)
    {
        var filePart = FindResource(partType);
        if (filePart == null || filePart.Size == 0)
            return null;

        filePart.Decrypt(mapContent, filePart.Size);
        return filePart;
    }

    MapResourceInfo FindAndDecryptFilePartSafe(MapFilePartType partType)
    {
        return FindAndDecryptFilePart(partType)
            ?? throw new InvalidDataException($"No {partType} information available in mapfile!");
    }

    int ReadBeInt(int offset)
    {
        return (mapContent[offset] & 0xFF)
            | ((mapContent[offset + 1] & 0xFF) << 8)
            | ((mapContent[offset + 2] & 0xFF) << 16)
            | ((mapContent[offset + 3] & 0xFF) << 24);
    }

    int ReadBeWord(int offset)
    {
        return (mapContent[offset] & 0xFF) | ((mapContent[offset + 1] & 0xFF) << 8);
    }

    string ReadCStr(int offset, int length)
    {
        if (mapContent.Length <= offset + length)
            return "";

        int i = 0;
        for (; i < length; i++)
        {
            if (mapContent[offset + i] == 0)
                break;
        }

        if (i == 0)
            return "";

        return Encoding.Latin1.GetString(mapContent, offset, i - 1);
    }
}
