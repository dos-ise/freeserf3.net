/*
 * DatFileReader.cs - Reader for the Settlers 3 GFX .dat files
 *
 * Ported from settlers-remake (MIT) AdvancedDatFileReader.java and
 * DatBitmapReader.java.
 *
 * The GFX files (Siedler3_NN.f8007e01f.dat = RGB565 or
 * Siedler3_NN.7c003e01f.dat = RGB555) contain all game images: landscape
 * tiles, settlers, buildings, torsos, shadows and GUI images. Images are
 * RLE-compressed per scanline.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Data;

/// <summary>Pixel color format of a dat file.</summary>
public enum DatFileType
{
    Rgb555,
    Rgb565
}

/// <summary>A decoded image (RGBA8, top-down rows).</summary>
public sealed class DatImage
{
    public int Width { get; }
    public int Height { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
    public byte[] Rgba { get; }

    public DatImage(int width, int height, int offsetX, int offsetY, byte[] rgba)
    {
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Rgba = rgba;
    }
}

/// <summary>
/// Reads a Settlers 3 GFX .dat file. Provides lazy access to the landscape,
/// settler, torso, shadow and GUI image sequences.
/// </summary>
public sealed class DatFileReader
{
    const int SequenceTypeCount = 6;
    const int IdSettlers = 0x106;
    const int IdTorsos = 0x3112;
    const int IdLandscape = 0x2412;
    const int IdShadows = 0x5982;
    const int IdGuis = 0x11306;

    static readonly byte[] FileStart1 =
    {
        0x04, 0x13, 0x04, 0x00, 0x0c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x54, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00
    };

    static readonly byte[] FileStart2 = { 0x00, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    static readonly byte[] FileHeaderEnd = { 0x04, 0x19, 0x00, 0x00, 0x0c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    static readonly byte[] SequenceStart = { 0x02, 0x14, 0x00, 0x00, 0x08, 0x00, 0x00 };

    readonly byte[] data;
    readonly DatFileType type;
    readonly string fileName;

    int[] settlerStarts = [];
    int[] torsoStarts = [];
    int[] shadowStarts = [];
    int[] landscapeStarts = [];
    int[] guiStarts = [];

    DatImage?[]? landscapeImages;
    DatImage?[]? guiImages;
    DatImage?[][]? settlerSequences;
    DatImage?[][]? torsoSequences;
    DatImage?[][]? shadowSequences;
    bool initialized;

    public DatFileReader(byte[] data, DatFileType type, string fileName)
    {
        this.data = data;
        this.type = type;
        this.fileName = fileName;
    }

    public string FileName => fileName;

    public static DatFileType GetTypeForPath(string path)
    {
        if (path.EndsWith(".7c003e01f.dat", StringComparison.OrdinalIgnoreCase))
            return DatFileType.Rgb555;
        if (path.EndsWith(".f8007e01f.dat", StringComparison.OrdinalIgnoreCase))
            return DatFileType.Rgb565;
        throw new ArgumentException($"Could not determine dat file type of {path}");
    }

    void Initialize()
    {
        if (initialized)
            return;

        try
        {
            var reader = new ByteReader(data);
            ReadSequenceIndexStarts(reader);

            for (int i = 0; i < SequenceTypeCount; i++)
            {
                try
                {
                    ReadSequencesAt(reader, sequenceIndexStarts[i]);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Error while loading sequence: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            // Some dat files (e.g. empty placeholder files) do not have the
            // standard header. Treat them as empty, like the Java reference.
            Console.Error.WriteLine($"Could not read dat file {fileName}: {e.Message}");
            settlerStarts = [];
            torsoStarts = [];
            shadowStarts = [];
            landscapeStarts = [];
            guiStarts = [];
        }

        landscapeImages = new DatImage?[landscapeStarts.Length];
        guiImages = new DatImage?[guiStarts.Length];
        settlerSequences = new DatImage?[settlerStarts.Length][];
        torsoSequences = new DatImage?[settlerStarts.Length][];
        shadowSequences = new DatImage?[settlerStarts.Length][];

        // Align torsos and shadows to the settler count (see Java reference).
        if (torsoStarts.Length < settlerStarts.Length)
        {
            int difference = settlerStarts.Length - torsoStarts.Length;
            var old = torsoStarts;
            torsoStarts = new int[settlerStarts.Length];
            Array.Copy(old, 0, torsoStarts, difference, old.Length);
            for (int i = 0; i < difference; i++)
                torsoStarts[i] = -1;
        }

        if (shadowStarts.Length < settlerStarts.Length)
        {
            int difference = settlerStarts.Length - shadowStarts.Length;
            var old = shadowStarts;
            shadowStarts = new int[settlerStarts.Length];
            if (difference == 8 || difference == 7)
            {
                for (int i = 0; i < difference; i++)
                    shadowStarts[i] = -1;
                for (int i = difference; i < settlerStarts.Length; i++)
                    shadowStarts[i] = old[i - difference];
            }
            else
            {
                for (int i = 0; i < old.Length; i++)
                    shadowStarts[i] = old[i];
                for (int i = old.Length; i < settlerStarts.Length; i++)
                    shadowStarts[i] = -1;
            }
        }

        initialized = true;
    }

    int[] sequenceIndexStarts = new int[SequenceTypeCount];

    void ReadSequenceIndexStarts(ByteReader reader)
    {
        reader.AssumeToRead(FileStart1);
        reader.AssumeToRead(GetFileStartMagic());
        reader.AssumeToRead(FileStart2);

        int fileSize = reader.Read32();
        if (fileSize != data.Length)
        {
            throw new InvalidDataException(
                $"The length stored in the dat file ({fileSize}) is not the file length ({data.Length}).");
        }

        reader.Read32(); // unknown

        for (int i = 0; i < SequenceTypeCount; i++)
            sequenceIndexStarts[i] = reader.Read32();

        reader.Read32(); // unknown
        reader.AssumeToRead(FileHeaderEnd);
    }

    byte[] GetFileStartMagic()
    {
        return type == DatFileType.Rgb565
            ? new byte[] { 0xf8, 0x00, 0x00, 0xe0, 0x07 }
            : new byte[] { 0x7c, 0x00, 0x00, 0xe0, 0x03 };
    }

    void ReadSequencesAt(ByteReader reader, int sequenceIndexStart)
    {
        reader.SkipTo(sequenceIndexStart);

        int sequenceType = reader.Read32();
        int byteCount = reader.Read16();
        int pointerCount = reader.Read16();

        if (byteCount != pointerCount * 4 + 8)
        {
            throw new InvalidDataException(
                $"Sequence index block length ({pointerCount}) and bytecount ({byteCount}) are not consistent.");
        }

        var pointers = new int[pointerCount];
        for (int i = 0; i < pointerCount; i++)
            pointers[i] = reader.Read32();

        switch (sequenceType)
        {
            case IdSettlers: settlerStarts = pointers; break;
            case IdTorsos: torsoStarts = pointers; break;
            case IdLandscape: landscapeStarts = pointers; break;
            case IdShadows: shadowStarts = pointers; break;
            case IdGuis: guiStarts = pointers; break;
        }
    }

    /// <summary>Number of landscape images in this file.</summary>
    public int LandscapeCount
    {
        get { Initialize(); return landscapeStarts.Length; }
    }

    /// <summary>Number of settler sequences in this file.</summary>
    public int SettlerCount
    {
        get { Initialize(); return settlerStarts.Length; }
    }

    /// <summary>Number of GUI images in this file.</summary>
    public int GuiCount
    {
        get { Initialize(); return guiStarts.Length; }
    }

    /// <summary>Gets a landscape image (lazily decoded).</summary>
    public DatImage? GetLandscape(int index)
    {
        Initialize();
        if (index < 0 || index >= landscapeStarts.Length)
            return null;
        if (landscapeImages![index] == null)
        {
            try
            {
                landscapeImages[index] = GetImage(landscapeStarts[index], HeaderType.Landscape, $"{fileName}-L{index}");
            }
            catch
            {
                landscapeImages[index] = null;
            }
        }
        return landscapeImages[index];
    }

    /// <summary>Gets a GUI image (lazily decoded).</summary>
    public DatImage? GetGui(int index)
    {
        Initialize();
        if (index < 0 || index >= guiStarts.Length)
            return null;
        if (guiImages![index] == null)
        {
            try
            {
                guiImages[index] = GetImage(guiStarts[index], HeaderType.Gui, $"{fileName}-G{index}");
            }
            catch
            {
                guiImages[index] = null;
            }
        }
        return guiImages[index];
    }

    /// <summary>Gets all frames of a settler sequence (lazily decoded).</summary>
    public DatImage[] GetSettlerSequence(int index)
    {
        Initialize();
        if (index < 0 || index >= settlerStarts.Length)
            return [];
        if (settlerSequences![index] == null)
        {
            try
            {
                settlerSequences[index] = LoadSettlers(index);
            }
            catch
            {
                settlerSequences[index] = [];
            }
        }
        return settlerSequences[index]!;
    }

    DatImage[] LoadSettlers(int goldIndex)
    {
        int position = settlerStarts[goldIndex];
        long[] framePositions = ReadSequenceHeader(position);

        var images = new DatImage[framePositions.Length];
        for (int i = 0; i < framePositions.Length; i++)
        {
            images[i] = GetImage(framePositions[i], HeaderType.Displaced, $"{fileName}-S{goldIndex}:{i}");
        }

        int torsoPosition = torsoStarts[goldIndex];
        if (torsoPosition >= 0)
        {
            long[] torsoPositions = ReadSequenceHeader(torsoPosition);
            var torsos = new DatImage[Math.Min(torsoPositions.Length, framePositions.Length)];
            for (int i = 0; i < torsos.Length; i++)
            {
                // Torso: 8-bit grayscale mask (TorsoTranslator).
                torsos[i] = GetImage(torsoPositions[i], HeaderType.Displaced, $"{fileName}-T{goldIndex}:{i}", isTorso: true);
            }
            torsoSequences![goldIndex] = torsos;
        }

        int shadowPosition = shadowStarts[goldIndex];
        if (shadowPosition >= 0)
        {
            long[] shadowPositions = ReadSequenceHeader(shadowPosition);
            var shadows = new DatImage[Math.Min(shadowPositions.Length, framePositions.Length)];
            for (int i = 0; i < shadows.Length; i++)
            {
                // Shadows are a mask: non-transparent pixels become a
                // semi-transparent black (alpha 0.5), see ShadowTranslator.
                shadows[i] = GetImage(shadowPositions[i], HeaderType.Displaced, $"{fileName}-SH{goldIndex}:{i}", unchecked((int)0x88000000));
            }
            shadowSequences![goldIndex] = shadows;
        }

        return images;
    }

    /// <summary>Gets the torso (color mask) frames of a settler sequence.</summary>
    public DatImage[] GetSettlerTorsoSequence(int index)
    {
        Initialize();
        if (index < 0 || index >= settlerStarts.Length)
            return [];
        if (settlerSequences![index] == null)
        {
            try { settlerSequences[index] = LoadSettlers(index); }
            catch { settlerSequences[index] = []; }
        }
        return torsoSequences![index] ?? [];
    }

    /// <summary>Gets the shadow frames of a settler sequence.</summary>
    public DatImage[] GetSettlerShadowSequence(int index)
    {
        Initialize();
        if (index < 0 || index >= settlerStarts.Length)
            return [];
        if (settlerSequences![index] == null)
        {
            try { settlerSequences[index] = LoadSettlers(index); }
            catch { settlerSequences[index] = []; }
        }
        return shadowSequences![index] ?? [];
    }

    long[] ReadSequenceHeader(int position)
    {
        var reader = new ByteReader(data);
        reader.SkipTo(position);
        reader.AssumeToRead(SequenceStart);

        int frameCount = reader.Read8();
        var framePositions = new long[frameCount];
        for (int i = 0; i < frameCount; i++)
            framePositions[i] = reader.Read32() + position;
        return framePositions;
    }

    enum HeaderType
    {
        Displaced,
        Landscape,
        Gui
    }

    DatImage GetImage(long position, HeaderType headerType, string name, int untransparentColor = -1, bool isTorso = false)
    {
        var reader = new ByteReader(data);
        reader.SkipTo(position);

        if (headerType == HeaderType.Displaced)
            reader.AssumeToRead(new byte[] { 0x0c, 0, 0, 0 });

        int width = reader.Read16();
        int height = reader.Read16();

        int offsetX = 0;
        int offsetY = 0;
        if (headerType == HeaderType.Displaced)
        {
            offsetX = reader.Read16Signed();
            offsetY = reader.Read16Signed();
        }
        else if (headerType == HeaderType.Gui)
        {
            reader.Read16();
            reader.Read16();
        }
        else
        {
            reader.Read16();
        }

        if (reader.Position % 2 == 1)
            reader.Read8();

        byte[] rgba = ReadCompressedData(reader, width, height, untransparentColor, isTorso);
        return new DatImage(width, height, offsetX, offsetY, rgba);
    }

    byte[] ReadCompressedData(ByteReader reader, int width, int height, int untransparentColor = -1, bool isTorso = false)
    {
        var rgba = new byte[width * height * 4];
        int transparent = 0;

        for (int y = 0; y < height; y++)
        {
            bool newLine = false;
            int x = 0;

            while (!newLine)
            {
                int currentMeta = reader.Read16();

                int sequenceLength = currentMeta & 0xff;
                int skip = (currentMeta & 0x7f00) >> 8;
                newLine = (currentMeta & 0x8000) != 0;

                int skipEnd = x + skip;
                while (x < skipEnd && x < width)
                {
                    WritePixel(rgba, width, x, y, transparent);
                    x++;
                }

                int readPartEnd = x + sequenceLength;
                while (x < readPartEnd && x < width)
                {
                    int color;
                    if (isTorso)
                    {
                        // Torso: 8-bit grayscale mask, 5-bit effective
                        // (TorsoTranslator). Stored opaque with the tint
                        // intensity in the blue channel.
                        int read = (reader.Read8() & 0x1f) * 255 / 31;
                        color = (0xff << 24) | read;
                    }
                    else if (untransparentColor != -1)
                    {
                        // Shadow: a pure mask without pixel data; every
                        // non-transparent pixel is the same color
                        // (ShadowTranslator returns 136 = alpha 0.5).
                        color = untransparentColor;
                    }
                    else
                    {
                        color = ConvertTo8888(reader.Read16());
                    }
                    WritePixel(rgba, width, x, y, color);
                    x++;
                }
            }
        }

        return rgba;
    }

    static void WritePixel(byte[] rgba, int width, int x, int y, int argb)
    {
        int index = (y * width + x) * 4;
        rgba[index] = (byte)(argb >> 16);       // R
        rgba[index + 1] = (byte)(argb >> 8);    // G
        rgba[index + 2] = (byte)argb;           // B
        rgba[index + 3] = (byte)(argb >> 24);   // A
    }

    int ConvertTo8888(int color)
    {
        if (type == DatFileType.Rgb565)
        {
            int r = Cnv5To8(color >> 11);
            int g = Cnv6To8(color >> 5);
            int b = Cnv5To8(color);
            return (0xff << 24) | (r << 16) | (g << 8) | b;
        }
        else
        {
            int r = Cnv5To8(color >> 10);
            int g = Cnv5To8(color >> 5);
            int b = Cnv5To8(color);
            return (0xff << 24) | (r << 16) | (g << 8) | b;
        }
    }

    static int Cnv5To8(int data) => (int)((data & 0x1F) * 255f / 31f);
    static int Cnv6To8(int data) => (int)((data & 0x3F) * 255f / 63f);
}
