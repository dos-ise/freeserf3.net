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
/// between them. Each building gets a flag; the player's central stock
/// flag is where produced goods are collected.
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

    /// <summary>The player's central stock flag, or null if not set.</summary>
    public Flag? StockFlag { get; private set; }

    /// <summary>Places the player's central stock flag.</summary>
    public Flag AddStockFlag(Position position, int player)
    {
        StockFlag = AddFlag(position, player);
        return StockFlag;
    }

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
    /// Removes a flag and all roads connected to it from the network
    /// (used when a building is demolished).
    /// </summary>
    public void RemoveFlag(Flag flag)
    {
        flags.Remove(flag);
        roads.RemoveAll(road => road.Start == flag || road.End == flag);
    }

    /// <summary>
    /// Connects two flags with a road served by a carrier. The path is
    /// found with the pathfinder; returns null if no path exists.
    /// </summary>
    public Road? Connect(Flag start, Flag end, int player, Func<Map, Position, bool>? isWalkable = null)
    {
        var path = Pathfinder.FindPath(map, start.Position, end.Position, isWalkable);
        if (path == null)
            return null;

        var road = new Road(start, end, path, player);
        roads.Add(road);
        return road;
    }

    /// <summary>
    /// Advances the network by one tick: the carriers walk along their
    /// roads and move goods between the flags.
    /// </summary>
    public void Tick()
    {
        foreach (var road in roads)
            road.Tick();
    }
}
