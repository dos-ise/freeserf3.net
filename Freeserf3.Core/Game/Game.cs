/*
 * Game.cs - The game state and its tick loop
 *
 * The Game owns the map, the players and the deterministic random
 * generator. The tick loop (heartbeat) advances the game clock; the
 * simulation steps (buildings, movables, ...) are added in later
 * milestones.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// The complete state of a running game: map, players, clock and random
/// generator.
/// </summary>
public sealed class Game
{
    public Game(Map map, ushort randomSeed)
    {
        Map = map;
        Random = new Random(randomSeed);
    }

    /// <summary>The game map.</summary>
    public Map Map { get; }

    /// <summary>The players of the game.</summary>
    public List<Player> Players { get; } = new();

    /// <summary>Deterministic random generator (seeded at game start).</summary>
    public Random Random { get; }

    /// <summary>The game clock, incremented once per tick.</summary>
    public ulong Tick { get; private set; }

    /// <summary>All buildings placed during play (not the map-file ones).</summary>
    public List<Building> Buildings { get; } = new();

    /// <summary>Creates the players from the map's player info.</summary>
    public void CreatePlayers()
    {
        Players.Clear();

        for (int i = 0; i < Map.Players.Count; i++)
        {
            var info = Map.Players[i];
            var player = new Player(i, info.Name)
            {
                Nation = info.Nation,
                StartPosition = new Position(info.StartX, info.StartY)
            };
            Players.Add(player);
        }
    }

    /// <summary>Gets the player with the given index, or null.</summary>
    public Player? GetPlayer(int index)
    {
        return index >= 0 && index < Players.Count ? Players[index] : null;
    }

    /// <summary>
    /// Places a new building for the given player. The building starts as a
    /// construction site and is built over time.
    /// </summary>
    public Building PlaceBuilding(BuildingType type, Position position, int playerIndex)
    {
        var building = new Building(type, position, playerIndex);
        Buildings.Add(building);
        return building;
    }

    /// <summary>
    /// Advances the game by one tick. The clock is incremented and all
    /// buildings are simulated (construction and production).
    /// </summary>
    public void TickOnce()
    {
        Tick++;

        foreach (var building in Buildings)
        {
            building.Tick(GetPlayer(building.Player));
        }
    }
}
