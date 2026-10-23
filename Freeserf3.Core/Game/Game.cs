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

    /// <summary>The walking settlers (created from the map's settlers).</summary>
    public List<Serf> Settlers { get; } = new();

    /// <summary>The resource stacks on the map (from the map file).</summary>
    public List<ResourceStack> Stacks { get; } = new();

    /// <summary>The settlers that collect resources from the stacks.</summary>
    public List<Serf> Collectors { get; } = new();

    /// <summary>The settlers that build the construction sites.</summary>
    public List<Serf> Builders { get; } = new();

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

        // Create walking settlers from the map's settlers.
        Settlers.Clear();
        Collectors.Clear();
        Builders.Clear();
        foreach (var mapSettler in Map.Settlers)
        {
            var serf = new Serf(mapSettler.Party, new Position(mapSettler.PosX, mapSettler.PosY), SerfType.Worker)
            {
                MoveInterval = 4
            };
            Settlers.Add(serf);
        }

        // Every third settler is a resource collector.
        for (int i = 0; i < Settlers.Count; i += 3)
        {
            Settlers[i].Type = SerfType.Collector;
            Collectors.Add(Settlers[i]);
        }

        // Create the resource stacks from the map file.
        Stacks.Clear();
        foreach (var mapStack in Map.Stacks)
        {
            var material = ResourceStack.FromMapStackType(mapStack.StackType);
            if (material == null)
                continue;
            Stacks.Add(new ResourceStack(new Position(mapStack.PosX, mapStack.PosY), material.Value, mapStack.Count));
        }

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
    /// the player's stock flag so produced goods can be carried away. A
    /// free settler is assigned as builder right away if one is available.
    /// </summary>
    public Building PlaceBuilding(BuildingType type, Position position, int playerIndex)
    {
        var building = new Building(type, position, playerIndex);
        Buildings.Add(building);

        if (playerIndex >= 0 && playerIndex < Networks.Count)
        {
            var network = Networks[playerIndex];
            var flag = network.AddFlag(FindFlagPosition(position), playerIndex);
            building.Flag = flag;

            if (network.StockFlag != null)
                network.Connect(flag, network.StockFlag, playerIndex, IsWalkable);
        }

        var player = GetPlayer(playerIndex);
        if (player != null && player.FreeSettlers > 0)
            AssignBuilder(building, player);

        return building;
    }

    /// <summary>
    /// Places a building for the human player near their start position so
    /// the construction site feature is visible right away (demo helper).
    /// Returns null if no suitable position was found.
    /// </summary>
    public Building? PlaceDemoBuilding(BuildingType type)
    {
        var player = GetPlayer(0);
        if (player == null)
            return null;

        // Search a few cells around the start position, keeping clear of
        // the stock flag's 3x3 footprint.
        for (int dx = -5; dx <= 5; dx++)
        {
            for (int dy = -5; dy <= 5; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 3)
                    continue;

                var pos = new Position(player.StartPosition.X + dx, player.StartPosition.Y + dy);
                if (!Map.IsInBounds(pos) || !IsWalkable(Map, pos))
                    continue;
                if (IsBuildingAt(pos))
                    continue;

                return PlaceBuilding(type, pos, 0);
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a walkable cell for the building's flag (its entrance). The
    /// flag is placed on the first walkable cell just outside the building's
    /// footprint so the road can connect even though the building blocks
    /// movement.
    /// </summary>
    Position FindFlagPosition(Position building)
    {
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 2)
                    continue; // inside the 3x3 footprint

                var pos = new Position(building.X + dx, building.Y + dy);
                if (Map.IsInBounds(pos) && IsWalkable(Map, pos))
                    return pos;
            }
        }

        return building;
    }

    /// <summary>Whether a carrier can walk on the given cell (not water, not a building).</summary>
    public bool IsWalkable(Map map, Position pos)
    {
        if (!map.IsInBounds(pos))
            return false;

        if (IsBuildingAt(pos))
            return false;

        var landscape = LandscapeMapping.FromMapValue(map.GetCell(pos).Landscape);
        return landscape is not (LandscapeType.Water1 or LandscapeType.Water2 or LandscapeType.Water3
            or LandscapeType.Water4 or LandscapeType.Water5 or LandscapeType.Water6
            or LandscapeType.Water7 or LandscapeType.Water8
            or LandscapeType.River1 or LandscapeType.River2 or LandscapeType.River3
            or LandscapeType.River4);
    }

    /// <summary>
    /// Whether the cell is occupied by a building footprint. Buildings
    /// block movement so carriers and settlers walk around them.
    /// </summary>
    public bool IsBuildingAt(Position pos)
    {
        foreach (var building in Buildings)
        {
            if (building.State == BuildingState.Destroyed)
                continue;
            if (BuildingFootprintContains(building.Position, pos))
                return true;
        }

        foreach (var building in Map.Buildings)
        {
            if (BuildingFootprintContains(new Position(building.PosX, building.PosY), pos))
                return true;
        }

        return false;
    }

    /// <summary>Whether the cell lies within a building's 3x3 footprint.</summary>
    static bool BuildingFootprintContains(Position building, Position pos)
    {
        return Math.Abs(pos.X - building.X) <= 1 && Math.Abs(pos.Y - building.Y) <= 1;
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
    /// Gives the settler a random walk target nearby and sets the path.
    /// Returns false if no reachable target was found.
    /// </summary>
    public bool Wander(Serf serf)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int dx = Random.Next(-8, 9);
            int dy = Random.Next(-8, 9);
            if (dx == 0 && dy == 0)
                continue;

            var target = new Position(serf.Position.X + dx, serf.Position.Y + dy);
            if (!Map.IsInBounds(target) || !IsWalkable(Map, target))
                continue;

            var path = Pathfinder.FindPath(Map, serf.Position, target, IsWalkable);
            if (path != null && path.Count > 0)
            {
                serf.SetPath(path);
                return true;
            }
        }
        return false;
    }

    /// <summary>Finds the nearest non-empty resource stack, or null.</summary>
    public ResourceStack? FindNearestStack(Position position)
    {
        ResourceStack? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (var stack in Stacks)
        {
            if (stack.IsEmpty)
                continue;

            int distance = MapGeometry.GetDistance(position, stack.Position);
            if (distance < nearestDistance)
            {
                nearest = stack;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    /// <summary>Gets the stock flag of the given player, or null.</summary>
    public Flag? GetStockFlag(int player)
    {
        if (player < 0 || player >= Networks.Count)
            return null;
        return Networks[player].StockFlag;
    }

    /// <summary>
    /// Advances the resource collectors: they walk to the nearest stack,
    /// pick up a good and carry it to the player's stock.
    /// </summary>
    void TickCollectors()
    {
        foreach (var collector in Collectors)
        {
            switch (collector.CollectorState)
            {
                case CollectorState.LookingForStack:
                    var stack = FindNearestStack(collector.Position);
                    if (stack == null)
                    {
                        collector.CollectorState = CollectorState.Pausing;
                        break;
                    }

                    collector.CollectorTarget = stack;
                    var path = Pathfinder.FindPath(Map, collector.Position, stack.Position, IsWalkable);
                    if (path != null && path.Count > 0)
                    {
                        collector.SetPath(path);
                        collector.CollectorState = CollectorState.WalkingToStack;
                    }
                    else
                    {
                        collector.CollectorState = CollectorState.Pausing;
                    }
                    break;

                case CollectorState.WalkingToStack:
                    collector.Tick();
                    if (collector.State != MovableState.Idle)
                        break;

                    // Arrived at the stack: pick up one good.
                    var target = collector.CollectorTarget;
                    if (target != null && !target.IsEmpty)
                    {
                        var material = target.TakeOne();
                        if (material.HasValue)
                        {
                            collector.PickUp(material.Value);
                            var stock = GetStockFlag(collector.Player);
                            if (stock != null)
                            {
                                var toStock = Pathfinder.FindPath(Map, collector.Position, stock.Position, IsWalkable);
                                if (toStock != null && toStock.Count > 0)
                                {
                                    collector.SetPath(toStock);
                                    collector.CollectorState = CollectorState.WalkingToStock;
                                    break;
                                }
                            }
                            // No stock reachable: drop the good again.
                            collector.Drop();
                        }
                    }
                    collector.CollectorState = CollectorState.LookingForStack;
                    break;

                case CollectorState.WalkingToStock:
                    collector.Tick();
                    if (collector.State != MovableState.Idle)
                        break;

                    // Arrived at the stock: drop the good into the player's stock.
                    var carried = collector.Drop();
                    if (carried.HasValue)
                    {
                        var player = GetPlayer(collector.Player);
                        if (player != null)
                            player.Inventory.Add(carried.Value);
                    }
                    collector.CollectorState = CollectorState.LookingForStack;
                    break;

                case CollectorState.Pausing:
                    collector.IdleTicks++;
                    if (collector.IdleTicks >= 30)
                    {
                        collector.IdleTicks = 0;
                        collector.CollectorState = CollectorState.LookingForStack;
                    }
                    break;
            }
        }
    }

    /// <summary>Ticks between two build actions of a builder.</summary>
    const int BuildActionTicks = 20;

    /// <summary>
    /// Assigns a free settler as builder of the given construction site.
    /// The builder appears at the site and starts building.
    /// </summary>
    void AssignBuilder(Building building, Player player)
    {
        player.FreeSettlers--;
        var builder = new Serf(player.Index, building.Position, SerfType.Builder)
        {
            ConstructionSite = building,
            BuilderState = BuilderState.Building,
            MoveInterval = 4
        };
        Settlers.Add(builder);
        Builders.Add(builder);
    }

    /// <summary>
    /// Advances the builders: each builder consumes one construction
    /// material from its site every few ticks. A finished or destroyed site
    /// releases the builder back to the free settlers.
    /// </summary>
    void TickBuilders()
    {
        for (int i = Builders.Count - 1; i >= 0; i--)
        {
            var builder = Builders[i];
            var site = builder.ConstructionSite;

            if (site == null || site.State != BuildingState.ConstructionSite)
            {
                ReleaseBuilder(builder);
                continue;
            }

            switch (builder.BuilderState)
            {
                case BuilderState.WalkingToSite:
                    builder.Tick();
                    if (builder.State == MovableState.Idle)
                        builder.BuilderState = BuilderState.Building;
                    break;

                case BuilderState.Building:
                    builder.BuildTicks++;
                    if (builder.BuildTicks >= BuildActionTicks)
                    {
                        builder.BuildTicks = 0;
                        site.ConsumeConstructionMaterial();
                    }
                    break;
            }
        }
    }

    /// <summary>Releases a builder: it becomes a free settler again.</summary>
    void ReleaseBuilder(Serf builder)
    {
        var player = GetPlayer(builder.Player);
        if (player != null)
            player.FreeSettlers++;
        builder.ConstructionSite = null;
        builder.Type = SerfType.Worker;
        Builders.Remove(builder);
    }

    /// <summary>
    /// Whether the player still needs the given material for one of its
    /// construction sites. Such materials stay at the stock flag so the
    /// carriers can deliver them.
    /// </summary>
    bool IsConstructionMaterialNeeded(Player player, Material material)
    {
        foreach (var building in Buildings)
        {
            if (building.Player != player.Index)
                continue;
            if (building.State != BuildingState.ConstructionSite)
                continue;
            if (building.DeliveredMaterials.Get(material) < building.Definition.GetConstructionCost(material))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Advances the game by one tick. The clock is incremented and all
    /// buildings are simulated (worker assignment and production); produced
    /// goods are queued at the building flags and carried to the player's
    /// stock by the carriers. Construction sites receive their materials
    /// through the transport network and are built by their builders.
    /// Living houses grow the population.
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

            // Leftover construction materials of finished buildings go back
            // to the player's stock.
            if (building.State != BuildingState.ConstructionSite && !building.DeliveredMaterials.IsEmpty && player != null)
            {
                foreach (var (material, count) in building.DeliveredMaterials.NonEmpty())
                {
                    player.Inventory.Add(material, count);
                    building.DeliveredMaterials.Remove(material, count);
                }
            }

            building.Tick();

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

        // Construction sites get a builder from the free settlers.
        foreach (var building in Buildings)
        {
            if (building.State != BuildingState.ConstructionSite)
                continue;
            if (Builders.Any(b => b.ConstructionSite == building))
                continue;

            var player = GetPlayer(building.Player);
            if (player == null || player.FreeSettlers <= 0)
                continue;

            AssignBuilder(building, player);
        }

        // Construction materials that arrived at the building flags are
        // moved into the delivered materials before the carriers can take
        // them back to the stock.
        foreach (var building in Buildings)
        {
            if (building.State != BuildingState.ConstructionSite || building.Flag == null)
                continue;

            foreach (var (material, count) in building.Definition.ConstructionCosts)
            {
                while (building.DeliveredMaterials.Get(material) < count && building.Flag.Contains(material))
                {
                    building.Flag.TakeGood(material);
                    building.DeliveredMaterials.Add(material);
                }
            }
        }

        // Goods that arrived at the stock flags go into the player's stock.
        // Materials still needed by a construction site stay at the flag so
        // the carriers can deliver them.
        for (int i = 0; i < Networks.Count; i++)
        {
            var player = GetPlayer(i);
            var stock = Networks[i].StockFlag;
            if (player == null || stock == null)
                continue;

            while (stock.Count > 0)
            {
                var good = stock.Peek();
                if (good.HasValue && IsConstructionMaterialNeeded(player, good.Value))
                    break;

                var taken = stock.TakeGood();
                if (taken.HasValue)
                    player.Inventory.Add(taken.Value);
            }
        }

        // Construction materials are sent from the player's stock to the
        // building flags through the transport network (one per site and
        // tick); the carriers bring them to the construction sites.
        for (int i = 0; i < Networks.Count; i++)
        {
            var player = GetPlayer(i);
            var stock = Networks[i].StockFlag;
            if (player == null || stock == null)
                continue;

            foreach (var building in Buildings)
            {
                if (building.Player != i || building.State != BuildingState.ConstructionSite)
                    continue;

                foreach (var (material, count) in building.Definition.ConstructionCosts)
                {
                    if (building.DeliveredMaterials.Get(material) >= count)
                        continue;
                    if (player.Inventory.Get(material) <= 0)
                        continue;

                    player.Inventory.Remove(material);
                    stock.AddGood(material);
                    break;
                }
            }
        }

        // Carriers walk along the roads and move the goods.
        foreach (var network in Networks)
            network.Tick();

        // Settlers wander around the map; after a short pause they pick a
        // new walk target. Collectors and builders are driven by their own
        // state machines.
        foreach (var settler in Settlers)
        {
            if (settler.Type is SerfType.Collector or SerfType.Builder)
                continue;
            settler.Tick();
            if (settler.State == MovableState.Idle)
            {
                settler.IdleTicks++;
                if (settler.IdleTicks >= 30)
                {
                    settler.IdleTicks = 0;
                    Wander(settler);
                }
            }
        }

        // Resource collectors walk to stacks, pick up goods and carry them
        // to the player's stock.
        TickCollectors();

        // Builders build the construction sites.
        TickBuilders();

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
