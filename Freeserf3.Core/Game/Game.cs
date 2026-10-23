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

using System.Linq;

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

    /// <summary>Population needed to win the round.</summary>
    public int TargetPopulation { get; set; } = 20;

    /// <summary>Whether the round has been won.</summary>
    public bool IsWon { get; private set; }

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

    /// <summary>
    /// Starts a new round: creates the players, gives them starting
    /// resources and sets the initial population from the map's settlers.
    /// </summary>
    public void StartGame()
    {
        CreatePlayers();
        Buildings.Clear();
        IsWon = false;

        foreach (var player in Players)
        {
            // Starting stock so the first buildings can be constructed.
            player.Inventory.Set(Material.Plank, 10);
            player.Inventory.Set(Material.Stone, 10);

            // The map's settlers of this player form the initial population.
            int count = Map.Settlers.Count(s => s.Party == player.Index);
            player.Population = count;
            player.FreeSettlers = count;
        }

        // The win target is the starting population plus a growth goal, so
        // the round is not won immediately on maps with many pre-placed
        // settlers.
        var human = GetPlayer(0);
        if (human != null)
            TargetPopulation = human.Population + 20;
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
    /// buildings are simulated (construction, worker assignment and
    /// production); living houses grow the population.
    /// </summary>
    public void TickOnce()
    {
        Tick++;

        foreach (var building in Buildings)
        {
            var player = GetPlayer(building.Player);
            building.Tick(player);

            // A finished building gets a worker from the free settlers.
            if (building.State == BuildingState.Active
                && !building.HasWorker
                && building.Definition.NeedsWorker
                && player != null
                && player.FreeSettlers > 0)
            {
                player.FreeSettlers--;
                building.AssignWorker(building.Definition.Worker);
            }

            // Produced goods go straight to the player's stock (transport
            // via carriers is a later milestone).
            if (player != null && building.Output.TotalCount > 0)
            {
                foreach (var (material, count) in building.Output.NonEmpty())
                {
                    player.Inventory.Add(material, count);
                    building.Output.Remove(material, count);
                }
            }
        }

        // Living houses grow the population over time.
        if (Tick % 600 == 0)
        {
            foreach (var building in Buildings)
            {
                if (building.State != BuildingState.Active || !building.Definition.IsLivinghouse)
                    continue;

                var player = GetPlayer(building.Player);
                if (player == null)
                    continue;

                player.Population++;
                player.FreeSettlers++;
            }
        }

        // Win condition: the first player to reach the target population.
        if (!IsWon)
        {
            var human = GetPlayer(0);
            if (human != null && human.Population >= TargetPopulation)
                IsWon = true;
        }
    }
}
