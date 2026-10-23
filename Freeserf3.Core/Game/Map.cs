/*
 * Map.cs - The game map with full per-cell state
 *
 * The Map wraps the terrain data loaded by MapFileReader and adds the
 * mutable game state: roads, flags and the objects placed during play.
 * Cell state is stored in a flat array indexed by (y * Size + x).
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;

namespace Freeserf3.Simulation;

/// <summary>State of a road on a map cell.</summary>
public enum RoadState : byte
{
    None = 0,
    Road = 1
}

/// <summary>Full mutable state of one map cell.</summary>
public struct MapCell
{
    public byte Height;
    public byte Landscape;
    public byte MapObject;
    public byte Player;
    public byte Accessible;
    public byte ResourceType1;
    public byte ResourceType2;

    /// <summary>Road state (transport network, see M2.4).</summary>
    public RoadState Road;

    /// <summary>Whether a flag is placed on this cell.</summary>
    public bool HasFlag;
}

/// <summary>
/// The game map: terrain grid plus the buildings, settlers and stacks that
/// were placed on the map when it was created.
/// </summary>
public sealed class Map
{
    readonly MapCell[] cells;
    readonly int size;

    public Map(int size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Map size must be positive.");

        this.size = size;
        cells = new MapCell[size * size];
    }

    /// <summary>Creates a game map from a loaded map file reader.</summary>
    public static Map FromReader(MapFileReader reader)
    {
        if (reader.Area == null)
            throw new InvalidOperationException("Map area not loaded. Call ReadMapData() first.");

        var map = new Map(reader.WidthHeight)
        {
            PlayerCount = reader.PlayerCount,
            SinglePlayerMap = reader.SinglePlayerMap,
            StartResources = reader.StartResources,
            QuestText = reader.MapQuestText,
            QuestTip = reader.MapQuestTip
        };

        for (int i = 0; i < reader.Area.Length; i++)
        {
            var node = reader.Area[i];
            map.cells[i] = new MapCell
            {
                Height = node.Height,
                Landscape = node.Landscape,
                MapObject = node.MapObject,
                Player = node.Player,
                Accessible = node.Accessible,
                ResourceType1 = node.ResourceType1,
                ResourceType2 = node.ResourceType2
            };
        }

        map.Buildings.AddRange(reader.Buildings);
        map.Settlers.AddRange(reader.Settlers);
        map.Stacks.AddRange(reader.Stacks);
        map.Players.AddRange(reader.Players);

        return map;
    }

    /// <summary>Map size in cells (the map is square).</summary>
    public int Size => size;

    /// <summary>Total number of cells.</summary>
    public int CellCount => size * size;

    public int PlayerCount { get; set; }
    public bool SinglePlayerMap { get; set; }
    public int StartResources { get; set; }
    public string QuestText { get; set; } = "";
    public string QuestTip { get; set; } = "";

    /// <summary>Buildings placed on the map (from the map file).</summary>
    public List<MapBuilding> Buildings { get; } = new();

    /// <summary>Settlers placed on the map (from the map file).</summary>
    public List<MapSettler> Settlers { get; } = new();

    /// <summary>Resource stacks placed on the map (from the map file).</summary>
    public List<MapStack> Stacks { get; } = new();

    /// <summary>Player start information (from the map file).</summary>
    public List<MapPlayerInfo> Players { get; } = new();

    /// <summary>Whether the position lies inside the map bounds.</summary>
    public bool IsInBounds(Position pos)
    {
        return (uint)pos.X < (uint)size && (uint)pos.Y < (uint)size;
    }

    /// <summary>Whether the position lies inside the map bounds.</summary>
    public bool IsInBounds(int x, int y)
    {
        return (uint)x < (uint)size && (uint)y < (uint)size;
    }

    /// <summary>Gets the cell at the given position.</summary>
    public MapCell GetCell(Position pos)
    {
        if (!IsInBounds(pos))
            throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is outside the map.");

        return cells[pos.Y * size + pos.X];
    }

    /// <summary>Gets the cell at the given position.</summary>
    public MapCell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Position ({x}, {y}) is outside the map.");

        return cells[y * size + x];
    }

    /// <summary>Sets the cell at the given position.</summary>
    public void SetCell(Position pos, in MapCell cell)
    {
        if (!IsInBounds(pos))
            throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is outside the map.");

        cells[pos.Y * size + pos.X] = cell;
    }

    /// <summary>Sets the cell at the given position.</summary>
    public void SetCell(int x, int y, in MapCell cell)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Position ({x}, {y}) is outside the map.");

        cells[y * size + x] = cell;
    }

    /// <summary>Enumerates all positions of the map in row-major order.</summary>
    public IEnumerable<Position> AllPositions()
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                yield return new Position(x, y);
        }
    }

    /// <summary>Enumerates all in-bounds neighbours of a position.</summary>
    public IEnumerable<Position> Neighbors(Position pos)
    {
        foreach (var direction in MapGeometry.AllDirections())
        {
            var neighbor = pos.GetNeighbor(direction);
            if (IsInBounds(neighbor))
                yield return neighbor;
        }
    }
}
