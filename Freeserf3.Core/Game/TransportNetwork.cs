/*
 * TransportNetwork.cs - Flags and roads of one player
 *
 * The transport network connects flags with roads. Goods are queued at
 * flags and transported along roads to neighbouring flags.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// Manages the flags and roads of a player and simulates the goods flow
/// between them.
/// </summary>
public sealed class TransportNetwork
{
    readonly Map map;
    readonly List<Flag> flags = new();
    readonly List<Road> roads = new();

    public TransportNetwork(Map map)
    {
        this.map = map;
    }

    /// <summary>All flags of the network.</summary>
    public IReadOnlyList<Flag> Flags => flags;

    /// <summary>All roads of the network.</summary>
    public IReadOnlyList<Road> Roads => roads;

    /// <summary>Places a flag at the given position.</summary>
    public Flag AddFlag(Position position, int player)
    {
        if (!map.IsInBounds(position))
            throw new ArgumentOutOfRangeException(nameof(position), "Flag position is outside the map.");

        var flag = new Flag(position, player);
        flags.Add(flag);
        return flag;
    }

    /// <summary>Gets the flag at the given position, or null.</summary>
    public Flag? GetFlag(Position position)
    {
        foreach (var flag in flags)
        {
            if (flag.Position == position)
                return flag;
        }
        return null;
    }

    /// <summary>
    /// Connects two flags with a road. The path is found with the
    /// pathfinder; returns null if no path exists.
    /// </summary>
    public Road? Connect(Flag start, Flag end, Func<Map, Position, bool>? isWalkable = null)
    {
        var path = Pathfinder.FindPath(map, start.Position, end.Position, isWalkable);
        if (path == null)
            return null;

        var road = new Road(start, end, path);
        roads.Add(road);
        return road;
    }

    /// <summary>
    /// Advances the network by one tick: goods move one step along each
    /// road.
    /// </summary>
    public void Tick()
    {
        foreach (var road in roads)
            road.Tick();
    }
}
