/*
 * MapModifier.cs - Terrain and object modification on the game map
 *
 * All changes go through the MapModifier so that the map can later notify
 * renderers and the simulation about changed cells.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Game;

/// <summary>
/// Provides operations to change the terrain and objects of a map:
/// heights, landscape, objects, ownership, resources, roads and flags.
/// </summary>
public sealed class MapModifier
{
    readonly Map map;

    public MapModifier(Map map)
    {
        this.map = map;
    }

    public Map Map => map;

    /// <summary>Sets the height of a cell.</summary>
    public void SetHeight(Position pos, byte height)
    {
        var cell = map.GetCell(pos);
        cell.Height = height;
        map.SetCell(pos, cell);
    }

    /// <summary>Sets the landscape value of a cell.</summary>
    public void SetLandscape(Position pos, byte landscape)
    {
        var cell = map.GetCell(pos);
        cell.Landscape = landscape;
        map.SetCell(pos, cell);
    }

    /// <summary>Sets the map object of a cell (tree, stone, ...).</summary>
    public void SetObject(Position pos, byte mapObject)
    {
        var cell = map.GetCell(pos);
        cell.MapObject = mapObject;
        map.SetCell(pos, cell);
    }

    /// <summary>Sets the owning player of a cell.</summary>
    public void SetPlayer(Position pos, byte player)
    {
        var cell = map.GetCell(pos);
        cell.Player = player;
        map.SetCell(pos, cell);
    }

    /// <summary>Sets the accessibility flag of a cell.</summary>
    public void SetAccessible(Position pos, byte accessible)
    {
        var cell = map.GetCell(pos);
        cell.Accessible = accessible;
        map.SetCell(pos, cell);
    }

    /// <summary>Sets the two resource types of a cell.</summary>
    public void SetResources(Position pos, byte resourceType1, byte resourceType2)
    {
        var cell = map.GetCell(pos);
        cell.ResourceType1 = resourceType1;
        cell.ResourceType2 = resourceType2;
        map.SetCell(pos, cell);
    }

    /// <summary>Places a road on a cell.</summary>
    public void PlaceRoad(Position pos)
    {
        var cell = map.GetCell(pos);
        cell.Road = RoadState.Road;
        map.SetCell(pos, cell);
    }

    /// <summary>Removes a road from a cell.</summary>
    public void RemoveRoad(Position pos)
    {
        var cell = map.GetCell(pos);
        cell.Road = RoadState.None;
        map.SetCell(pos, cell);
    }

    /// <summary>Places a flag on a cell.</summary>
    public void SetFlag(Position pos, bool hasFlag)
    {
        var cell = map.GetCell(pos);
        cell.HasFlag = hasFlag;
        map.SetCell(pos, cell);
    }

    /// <summary>Flattens a cell to the given height (used when building).</summary>
    public void Flatten(Position pos, byte height)
    {
        var cell = map.GetCell(pos);
        cell.Height = height;
        cell.Accessible = 1;
        map.SetCell(pos, cell);
    }
}
