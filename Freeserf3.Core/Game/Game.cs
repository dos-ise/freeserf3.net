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
using Freeserf3.Data;

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

    /// <summary>The transport network of each player (index = player index).</summary>
    public List<TransportNetwork> Networks { get; } = new();

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
        Networks.Clear();
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

            // The player's transport network with its central stock flag.
            var network = new TransportNetwork(Map);
            network.AddStockFlag(player.StartPosition, player.Index);
            Networks.Add(network);
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
    /// construction site and is built over time. Its flag is connected to
    /// the player's stock flag so produced goods can be carried away.
    /// </summary>
    public Building PlaceBuilding(BuildingType type, Position position, int playerIndex)
    {
        var building = new Building(type, position, playerIndex);
        Buildings.Add(building);

        if (playerIndex >= 0 && playerIndex < Networks.Count)
        {
            var network = Networks[playerIndex];
            var flag = network.AddFlag(position, playerIndex);
            building.Flag = flag;

            if (network.StockFlag != null)
                network.Connect(flag, network.StockFlag, playerIndex, IsWalkable);
        }

        return building;
    }

    /// <summary>Whether a carrier can walk on the given cell (not water).</summary>
    public bool IsWalkable(Map map, Position pos)
    {
        if (!map.IsInBounds(pos))
            return false;

        var landscape = LandscapeMapping.FromMapValue(map.GetCell(pos).Landscape);
        return landscape is not (LandscapeType.Water1 or LandscapeType.Water2 or LandscapeType.Water3
            or LandscapeType.Water4 or LandscapeType.Water5 or LandscapeType.Water6
            or LandscapeType.Water7 or LandscapeType.Water8);
    }

    /// <summary>All carriers of all players (for rendering).</summary>
    public IEnumerable<Serf> Carriers
    {
        get
        {
            foreach (var network in Networks)
            {
                foreach (var road in network.Roads)
                    yield return road.Carrier;
            }
        }
    }

    /// <summary>
    /// Advances the game by one tick. The clock is incremented and all
    /// buildings are simulated (construction, worker assignment and
    /// production); produced goods are queued at the building flags and
    /// carried to the player's stock by the carriers. Living houses grow
    /// the population.
    /// </summary>
    public void TickOnce()
    {
        Tick++;

        // Buildings: deliver inputs, produce, assign workers, queue outputs.
        foreach (var building in Buildings)
        {
            var player = GetPlayer(building.Player);
            if (player != null)
            {
                // Deliver inputs from the player's stock (M3: directly;
                // carrier-based input delivery is a later milestone).
                foreach (var input in building.Definition.Inputs)
                {
                    if (building.Input.Get(input) <= 0 && player.Inventory.Get(input) > 0)
                    {
                        player.Inventory.Remove(input);
                        building.Input.Add(input);
                    }
                }
            }

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

            // Produced goods are queued at the building's flag for the
            // carrier to pick up.
            if (building.Flag != null && building.Output.TotalCount > 0)
            {
                foreach (var (material, count) in building.Output.NonEmpty())
                {
                    for (int i = 0; i < count; i++)
                        building.Flag.AddGood(material);
                    building.Output.Remove(material, count);
                }
            }
        }

        // Goods that arrived at the stock flags go into the player's stock.
        for (int i = 0; i < Networks.Count; i++)
        {
            var player = GetPlayer(i);
            var stock = Networks[i].StockFlag;
            if (player == null || stock == null)
                continue;

            while (stock.Count > 0)
            {
                var good = stock.TakeGood();
                if (good.HasValue)
                    player.Inventory.Add(good.Value);
            }
        }

        // Carriers walk along the roads and move the goods.
        foreach (var network in Networks)
            network.Tick();

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
