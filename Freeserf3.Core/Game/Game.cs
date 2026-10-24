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

    /// <summary>The settlers that gather map resources (lumberjacks, stonecutters).</summary>
    public List<Serf> ResourceWorkers { get; } = new();

    /// <summary>Positions of the trees still standing on the map.</summary>
    public HashSet<Position> Trees { get; } = new();

    /// <summary>Positions of the stones still on the map.</summary>
    public HashSet<Position> Stones { get; } = new();

    /// <summary>The transport network of each player (index = player index).</summary>
    public List<TransportNetwork> Networks { get; } = new();

    /// <summary>Population needed to win the round.</summary>
    public int TargetPopulation { get; set; } = 20;

    /// <summary>Whether the round has been won.</summary>
    public bool IsWon { get; private set; }

    /// <summary>
    /// Radius in which a military building claims territory around itself
    /// (settlers-remake CommonConstants.TOWER_RADIUS).
    /// </summary>
    public const int TerritoryRadius = 40;

    /// <summary>
    /// Radius in which a military building attacks enemy settlers
    /// (settlers-remake Constants.TOWER_ATTACKABLE_SEARCH_RADIUS).
    /// </summary>
    public const int AttackRadius = 40;

    /// <summary>Per-cell territory owner (index = y * Size + x), 0 = no owner.</summary>
    byte[] territory = Array.Empty<byte>();

    /// <summary>
    /// Raised when a sound effect should be played (sound id, see
    /// SoundManager). Wired by the host to the sound player.
    /// </summary>
    public Action<int>? PlaySound { get; set; }

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

        // Create walking settlers from the map's settlers. In a
        // single-player round only the human player's settlers are part of
        // the game (no AI opponents). Settlers that the map places in
        // water are skipped (they would stand in the sea).
        Settlers.Clear();
        Collectors.Clear();
        Builders.Clear();
        ResourceWorkers.Clear();
        foreach (var mapSettler in Map.Settlers)
        {
            if (mapSettler.Party != 0)
                continue;

            var position = new Position(mapSettler.PosX, mapSettler.PosY);
            if (!IsWalkable(Map, position))
                continue;

            var serf = new Serf(mapSettler.Party, position, SerfType.Worker)
            {
                MoveInterval = 4
            };
            Settlers.Add(serf);
        }

        // A few settlers are resource collectors that bring the map's
        // stacks to the player's stock. Only a small share is needed (like
        // the real game, where carriers are assigned to the transport
        // network).
        for (int i = 0; i < Settlers.Count; i += 5)
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

        // Track the trees and stones on the map so they can be cut/mined.
        Trees.Clear();
        Stones.Clear();
        for (int y = 0; y < Map.Size; y++)
        {
            for (int x = 0; x < Map.Size; x++)
            {
                byte mapObject = Map.GetCell(x, y).MapObject;
                if (IsTreeObject(mapObject))
                    Trees.Add(new Position(x, y));
                else if (IsStoneObject(mapObject))
                    Stones.Add(new Position(x, y));
            }
        }

        foreach (var player in Players)
        {
            // Starting stock so the first buildings can be constructed. The
            // amount follows the map's start resources level (like
            // settlers-remake EMapStartResources).
            GiveStartResources(player);

            // The map's settlers of this player form the initial population.
            int count = Map.Settlers.Count(s => s.Party == player.Index);
            player.Population = count;
            player.FreeSettlers = count;

            // The player's transport network with its central stock flag.
            // The stock is placed at the player's base (their first
            // building on the map, or the start position if they have
            // none), so the base is not cut off by rivers or mountains.
            // The flag sits on a walkable cell next to the base building.
            var network = new TransportNetwork(Map);
            var basePos = GetPlayerBasePosition(player);
            var stockPos = IsBuildingAt(basePos) ? FindFlagPosition(basePos) : basePos;
            network.AddStockFlag(stockPos, player.Index);
            Networks.Add(network);
        }

        // The buildings placed on the map become real buildings: they are
        // active, connected to the transport network and staffed with the
        // soldiers the map file lists. In a single-player round only the
        // human player's buildings are part of the game (no AI opponents).
        LoadMapBuildings();

        // The win target is the starting population plus a growth goal, so
        // the round is not won immediately on maps with many pre-placed
        // settlers.
        var human = GetPlayer(0);
        if (human != null)
            TargetPopulation = human.Population + 20;

        RecomputeTerritory();
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
    /// Gets the position of the player's base: their first building on the
    /// map (nearest to the start position), or the start position if they
    /// have no building. The stock flag is placed here so the base is not
    /// cut off by rivers or mountains.
    /// </summary>
    Position GetPlayerBasePosition(Player player)
    {
        Position? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (var mapBuilding in Map.Buildings)
        {
            if (mapBuilding.Party != player.Index)
                continue;

            var pos = new Position(mapBuilding.PosX, mapBuilding.PosY);
            int distance = MapGeometry.GetDistance(pos, player.StartPosition);
            if (distance < nearestDistance)
            {
                nearest = pos;
                nearestDistance = distance;
            }
        }

        return nearest ?? player.StartPosition;
    }

    /// <summary>
    /// Loads the buildings placed on the map as real game buildings. They
    /// are active from the start, connected to the transport network and
    /// staffed with the soldiers the map file lists (like settlers-remake,
    /// where the map's buildings are part of the game). In a single-player
    /// round only the human player's buildings are loaded (no AI
    /// opponents).
    /// </summary>
    void LoadMapBuildings()
    {
        foreach (var mapBuilding in Map.Buildings)
        {
            if (mapBuilding.Party != 0)
                continue;

            var type = BuildingTypeFromMap(mapBuilding.BuildingType);
            if (type == null)
                continue;

            var building = new Building(type.Value, new Position(mapBuilding.PosX, mapBuilding.PosY), mapBuilding.Party);
            building.MarkActive();
            Buildings.Add(building);

            if (mapBuilding.Party >= 0 && mapBuilding.Party < Networks.Count)
            {
                var network = Networks[mapBuilding.Party];
                var flag = network.AddFlag(FindFlagPosition(building.Position), mapBuilding.Party);
                building.Flag = flag;

                if (network.StockFlag != null)
                    network.Connect(flag, network.StockFlag, mapBuilding.Party, IsWalkable);
            }

            // The soldiers the map file lists for this building.
            int soldiers = mapBuilding.CountSword1 + mapBuilding.CountSword2 + mapBuilding.CountSword3
                + mapBuilding.CountArcher1 + mapBuilding.CountArcher2 + mapBuilding.CountArcher3
                + mapBuilding.CountSpear1 + mapBuilding.CountSpear2 + mapBuilding.CountSpear3;
            for (int i = 0; i < soldiers && building.HasSoldierCapacity; i++)
                building.Soldiers.Add(new Serf(mapBuilding.Party, building.Position, SerfType.Soldier));
        }
    }

    /// <summary>
    /// Gives the player their starting goods. The amount follows the map's
    /// start resources level (1 = low, 2 = medium, 3 = high), like
    /// settlers-remake EMapStartResources.
    /// </summary>
    void GiveStartResources(Player player)
    {
        int level = Math.Clamp(Map.StartResources, 1, 3);
        int planks = level switch { 1 => 12, 2 => 25, _ => 30 };
        int stone = level switch { 1 => 12, 2 => 25, _ => 30 };
        int tools = level switch { 1 => 6, 2 => 12, _ => 16 };
        int weapons = level switch { 1 => 2, 2 => 5, _ => 8 };

        player.Inventory.Set(Material.Plank, planks);
        player.Inventory.Set(Material.Stone, stone);
        player.Inventory.Set(Material.Hammer, tools);
        player.Inventory.Set(Material.Axe, tools);
        player.Inventory.Set(Material.Pick, tools);
        player.Inventory.Set(Material.Saw, tools);
        player.Inventory.Set(Material.Scythe, tools);
        player.Inventory.Set(Material.FishingRod, tools);
        player.Inventory.Set(Material.Sword, weapons);
        player.Inventory.Set(Material.Bow, weapons);
        player.Inventory.Set(Material.Spear, weapons);
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

    /// <summary>
    /// Finds the nearest non-empty resource stack within the given radius,
    /// or null.
    /// </summary>
    public ResourceStack? FindNearestStack(Position position, int maxDistance = 30)
    {
        ResourceStack? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (var stack in Stacks)
        {
            if (stack.IsEmpty)
                continue;

            int distance = MapGeometry.GetDistance(position, stack.Position);
            if (distance > maxDistance || distance >= nearestDistance)
                continue;

            nearest = stack;
            nearestDistance = distance;
        }
        return nearest;
    }

    /// <summary>
    /// Whether a map-file object byte is a tree. Tree objects are 68-80
    /// (birch, elm, oak, ...) and 84 (small tree), see the original map
    /// object types.
    /// </summary>
    public static bool IsTreeObject(byte mapObject)
    {
        return mapObject is >= 68 and <= 80 or 84;
    }

    /// <summary>
    /// Whether a map-file object byte is a minable stone. The big stones
    /// are objects 1-8 (see the original map object types).
    /// </summary>
    public static bool IsStoneObject(byte mapObject)
    {
        return mapObject is >= 1 and <= 8;
    }

    /// <summary>
    /// Finds the nearest tree within the given radius (Manhattan distance),
    /// or null if there is none.
    /// </summary>
    public Position? FindNearestTree(Position position, int radius)
    {
        return FindNearestResource(Trees, position, radius);
    }

    /// <summary>
    /// Finds the nearest stone within the given radius (Manhattan distance),
    /// or null if there is none.
    /// </summary>
    public Position? FindNearestStone(Position position, int radius)
    {
        return FindNearestResource(Stones, position, radius);
    }

    static Position? FindNearestResource(HashSet<Position> resources, Position position, int radius)
    {
        Position? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (var resource in resources)
        {
            int distance = MapGeometry.GetDistance(position, resource);
            if (distance > radius || distance >= nearestDistance)
                continue;

            nearest = resource;
            nearestDistance = distance;
        }
        return nearest;
    }

    /// <summary>Removes a tree from the map (it has been cut down).</summary>
    public void CutTree(Position position)
    {
        Trees.Remove(position);
        PlaySound?.Invoke(36); // falling tree
    }

    /// <summary>Removes a stone from the map (it has been mined).</summary>
    public void MineStone(Position position)
    {
        Stones.Remove(position);
        PlaySound?.Invoke(3); // stonecutter
    }

    /// <summary>Plants a new tree on the map (forester).</summary>
    public void PlantTree(Position position)
    {
        if (!Map.IsInBounds(position))
            return;
        Trees.Add(position);
        var cell = Map.GetCell(position);
        cell.MapObject = 68; // birch tree
        Map.SetCell(position, cell);
    }

    /// <summary>Gets the stock flag of the given player, or null.</summary>
    public Flag? GetStockFlag(int player)
    {
        if (player < 0 || player >= Networks.Count)
            return null;
        return Networks[player].StockFlag;
    }

    /// <summary>
    /// Recomputes the territory grid from the military buildings. A cell is
    /// claimed by the player whose military buildings cover it most often
    /// (like the tower counter of settlers-remake); water cells are never
    /// claimed. Both the buildings placed during play and the military
    /// buildings from the map file claim territory. Called periodically by
    /// the tick loop.
    /// </summary>
    public void RecomputeTerritory()
    {
        int size = Map.Size;
        territory = new byte[size * size];
        if (Players.Count == 0)
            return;

        var counts = new int[Players.Count];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var pos = new Position(x, y);
                if (!IsWalkable(Map, pos))
                    continue;

                Array.Clear(counts, 0, counts.Length);
                foreach (var building in Buildings)
                {
                    if (building.State != BuildingState.Active || !building.Definition.IsMilitary)
                        continue;
                    if (building.Definition.SoldierCapacity <= 0)
                        continue; // barracks do not claim territory
                    if (MapGeometry.GetDistance(building.Position, pos) > TerritoryRadius)
                        continue;
                    if (building.Player >= 0 && building.Player < counts.Length)
                        counts[building.Player]++;
                }

                // The military buildings from the map file claim territory
                // too (like settlers-remake, where the map's towers occupy
                // their area from the start).
                foreach (var mapBuilding in Map.Buildings)
                {
                    var type = BuildingTypeFromMap(mapBuilding.BuildingType);
                    if (type == null)
                        continue;
                    var definition = BuildingDefinition.Get(type.Value);
                    if (!definition.IsMilitary || definition.SoldierCapacity <= 0)
                        continue;
                    if (MapGeometry.GetDistance(new Position(mapBuilding.PosX, mapBuilding.PosY), pos) > TerritoryRadius)
                        continue;
                    if (mapBuilding.Party >= 0 && mapBuilding.Party < counts.Length)
                        counts[mapBuilding.Party]++;
                }

                int best = 0;
                for (int p = 1; p < counts.Length; p++)
                {
                    if (counts[p] > counts[best])
                        best = p;
                }
                if (counts[best] > 0)
                    territory[y * size + x] = (byte)best;
            }
        }
    }

    /// <summary>
    /// Maps a map-file building type byte to a game building type. The map
    /// file uses the original game order (see settlers-remake
    /// EOriginalMapBuildingType).
    /// </summary>
    public static BuildingType? BuildingTypeFromMap(byte buildingType)
    {
        return buildingType switch
        {
            1 => BuildingType.Stock,
            2 => BuildingType.Lumberjack,
            3 => BuildingType.Stonecutter,
            4 => BuildingType.Sawmill,
            5 => BuildingType.Forester,
            6 => BuildingType.LookoutTower,
            7 => BuildingType.Coalmine,
            8 => BuildingType.Goldmine,
            9 => BuildingType.Ironmine,
            10 => BuildingType.Goldmelt,
            11 => BuildingType.Ironmelt,
            12 => BuildingType.Toolsmith,
            13 => BuildingType.Weaponsmith,
            14 => BuildingType.Winegrower,
            15 => BuildingType.Tower,
            16 => BuildingType.BigTower,
            17 => BuildingType.Mill,
            18 => BuildingType.Castle,
            19 => BuildingType.Barrack,
            20 => BuildingType.Baker,
            21 => BuildingType.Slaughterhouse,
            22 => BuildingType.Distillery,
            23 => BuildingType.PigFarm,
            24 => BuildingType.Farm,
            25 => BuildingType.Fisher,
            26 => BuildingType.SmallLivinghouse,
            27 => BuildingType.MediumLivinghouse,
            28 => BuildingType.BigLivinghouse,
            29 => BuildingType.Sulfurmine,
            30 => BuildingType.Waterworks,
            32 => BuildingType.Dockyard,
            33 => BuildingType.Harbor,
            34 => BuildingType.MarketPlace,
            35 => BuildingType.Hospital,
            36 => BuildingType.RiceFarm,
            37 => BuildingType.Gemsmine,
            38 => BuildingType.Brewery,
            39 => BuildingType.CharcoalBurner,
            41 => BuildingType.BigTemple,
            42 => BuildingType.Temple,
            43 => BuildingType.BigTemple,
            44 => BuildingType.Temple,
            45 => BuildingType.BigTemple,
            46 => BuildingType.Temple,
            49 => BuildingType.DonkeyFarm,
            51 => BuildingType.Beekeeping,
            52 => BuildingType.MeadBrewery,
            53 => BuildingType.Laboratory,
            54 => BuildingType.Temple,
            55 => BuildingType.BigTemple,
            56 => BuildingType.Sulfurmine,
            _ => null
        };
    }

    /// <summary>
    /// Gets the player who owns the territory at the given position, or 0
    /// if no military building claims it.
    /// </summary>
    public byte GetTerritoryPlayer(Position pos)
    {
        if (!Map.IsInBounds(pos))
            return 0;
        return territory[pos.Y * Map.Size + pos.X];
    }

    /// <summary>
    /// Advances the military: active military buildings recruit soldiers
    /// from free settlers with weapons and attack enemy settlers in range.
    /// </summary>
    void TickMilitary()
    {
        // Soldier recruitment: an active military building with free
        // capacity takes a free settler and a weapon from the player's
        // stock and stations the soldier in the building.
        foreach (var building in Buildings)
        {
            if (building.State != BuildingState.Active || !building.Definition.IsMilitary)
                continue;
            if (!building.HasSoldierCapacity)
                continue;

            var player = GetPlayer(building.Player);
            if (player == null || player.FreeSettlers <= 0)
                continue;
            if (player.Inventory.WeaponCount <= 0)
                continue;

            // Take one weapon (sword, spear or bow).
            Material weapon = MaterialExtensions.Weapons.First(w => player.Inventory.Get(w) > 0);
            player.Inventory.Remove(weapon);
            player.FreeSettlers--;

            building.Soldiers.Add(new Serf(building.Player, building.Position, SerfType.Soldier));
        }

        // Tower attacks: military buildings with soldiers shoot at enemy
        // settlers in range (checked every 30 ticks).
        if (Tick % 30 != 0)
            return;

        foreach (var building in Buildings)
        {
            if (building.State != BuildingState.Active || !building.Definition.IsMilitary)
                continue;
            if (building.Soldiers.Count <= 0)
                continue;

            var target = FindEnemyInRange(building);
            if (target == null)
                continue;

            // The enemy settler is defeated and removed from the game.
            Settlers.Remove(target);
            Collectors.Remove(target);
            Builders.Remove(target);
            ResourceWorkers.Remove(target);
        }
    }

    /// <summary>
    /// Finds the nearest enemy settler within the attack radius of the
    /// given military building, or null.
    /// </summary>
    Serf? FindEnemyInRange(Building building)
    {
        Serf? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (var settler in Settlers)
        {
            if (settler.Player == building.Player)
                continue;

            int distance = MapGeometry.GetDistance(building.Position, settler.Position);
            if (distance > AttackRadius || distance >= nearestDistance)
                continue;

            nearest = settler;
            nearestDistance = distance;
        }
        return nearest;
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

    /// <summary>Ticks between two work actions of a resource worker.</summary>
    const int ResourceWorkTicks = 30;

    /// <summary>
    /// Assigns a free settler as resource worker of the given production
    /// building (lumberjack, stonecutter). The worker appears at the
    /// building's flag and starts gathering resources. The required tool
    /// is consumed from the player's stock.
    /// </summary>
    void AssignResourceWorker(Building building, Player player)
    {
        player.FreeSettlers--;
        ConsumeWorkerTool(building, player);
        building.AssignWorker(building.Definition.Worker);
        var start = building.Flag?.Position ?? building.Position;
        var worker = new Serf(player.Index, start, SerfType.ResourceWorker)
        {
            Workplace = building,
            Profession = building.Definition.Worker,
            ResourceState = ResourceWorkerState.LookingForResource,
            MoveInterval = 4
        };
        Settlers.Add(worker);
        ResourceWorkers.Add(worker);
    }

    /// <summary>
    /// Whether the player has the tool required for the building's worker
    /// in their stock. Buildings whose profession works without a tool
    /// always qualify.
    /// </summary>
    bool HasWorkerTool(Player player, Building building)
    {
        if (building.Definition.Tool is not Material tool)
            return true;
        return player.Inventory.Get(tool) > 0;
    }

    /// <summary>
    /// Consumes the tool required for the building's worker from the
    /// player's stock (no-op for professions without a tool).
    /// </summary>
    void ConsumeWorkerTool(Building building, Player player)
    {
        if (building.Definition.Tool is Material tool)
            player.Inventory.Remove(tool);
    }

    /// <summary>
    /// Advances the resource workers: they look for a tree or stone in the
    /// work radius of their building, walk to it, cut/mine it and produce
    /// the building's output (following settlers-remake).
    /// </summary>
    void TickResourceWorkers()
    {
        for (int i = ResourceWorkers.Count - 1; i >= 0; i--)
        {
            var worker = ResourceWorkers[i];
            var building = worker.Workplace;

            if (building == null || building.State != BuildingState.Active)
            {
                ReleaseResourceWorker(worker);
                continue;
            }

            switch (worker.ResourceState)
            {
                case ResourceWorkerState.LookingForResource:
                    var resource = FindResourceFor(building, worker.Position);
                    if (resource == null)
                    {
                        worker.ResourceState = ResourceWorkerState.Pausing;
                        break;
                    }

                    worker.ResourceTarget = resource;
                    var path = Pathfinder.FindPath(Map, worker.Position, resource.Value, IsWalkable);
                    if (path != null && path.Count > 0)
                    {
                        worker.SetPath(path);
                        worker.ResourceState = ResourceWorkerState.WalkingToResource;
                    }
                    else
                    {
                        worker.ResourceState = ResourceWorkerState.Pausing;
                    }
                    break;

                case ResourceWorkerState.WalkingToResource:
                    worker.Tick();
                    if (worker.State != MovableState.Idle)
                        break;

                    // Arrived at the tree/stone: start cutting/mining.
                    worker.WorkTicks = 0;
                    worker.ResourceState = ResourceWorkerState.Working;
                    break;

                case ResourceWorkerState.Working:
                    // Face the tree/stone while cutting/mining/planting.
                    if (worker.ResourceTarget is Position resourceTarget)
                    {
                        var facing = worker.Position.GetDirectionTo(resourceTarget);
                        if (facing.HasValue)
                            worker.Facing = facing.Value;
                    }
                    worker.WorkTicks++;
                    if (worker.WorkTicks < ResourceWorkTicks)
                        break;

                    // Cut the tree, mine the stone or plant a new tree.
                    var target = worker.ResourceTarget;
                    if (target.HasValue)
                    {
                        if (building.Definition.Worker == WorkerType.Lumberjack)
                        {
                            CutTree(target.Value);
                            building.ProduceResource();
                        }
                        else if (building.Definition.Worker == WorkerType.Stonecutter)
                        {
                            MineStone(target.Value);
                            building.ProduceResource();
                        }
                        else if (building.Definition.Worker == WorkerType.Forester)
                        {
                            PlantTree(target.Value);
                        }
                    }
                    var home = building.Flag?.Position ?? building.Position;
                    var homePath = Pathfinder.FindPath(Map, worker.Position, home, IsWalkable);
                    if (homePath != null && homePath.Count > 0)
                    {
                        worker.SetPath(homePath);
                        worker.ResourceState = ResourceWorkerState.WalkingHome;
                    }
                    else
                    {
                        worker.ResourceState = ResourceWorkerState.LookingForResource;
                    }
                    break;

                case ResourceWorkerState.WalkingHome:
                    worker.Tick();
                    if (worker.State != MovableState.Idle)
                        break;

                    worker.ResourceTarget = null;
                    worker.ResourceState = ResourceWorkerState.LookingForResource;
                    break;

                case ResourceWorkerState.Pausing:
                    worker.IdleTicks++;
                    if (worker.IdleTicks >= 30)
                    {
                        worker.IdleTicks = 0;
                        worker.ResourceState = ResourceWorkerState.LookingForResource;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Finds the next tree or stone for the building's worker within the
    /// building's work radius, or null if none is left. Only reachable
    /// resources are considered (like settlers-remake preSearchPath). The
    /// forester looks for an empty walkable cell to plant a tree instead.
    /// </summary>
    Position? FindResourceFor(Building building, Position workerPosition)
    {
        int radius = building.Definition.WorkRadius;
        if (building.Definition.Worker == WorkerType.Lumberjack)
            return Pathfinder.FindNearest(Map, workerPosition, (m, p) => Trees.Contains(p), radius, IsWalkable);
        if (building.Definition.Worker == WorkerType.Stonecutter)
            return Pathfinder.FindNearest(Map, workerPosition, (m, p) => Stones.Contains(p), radius, IsWalkable);
        if (building.Definition.Worker == WorkerType.Forester)
            return Pathfinder.FindNearest(Map, workerPosition, (m, p) => IsPlantable(p) && p != workerPosition, radius, IsWalkable);
        return null;
    }

    /// <summary>Whether a tree can be planted on the cell (walkable and free).</summary>
    bool IsPlantable(Position pos)
    {
        return !Trees.Contains(pos) && !Stones.Contains(pos) && !IsBuildingAt(pos);
    }

    /// <summary>Releases a resource worker: it becomes a free settler again.</summary>
    void ReleaseResourceWorker(Serf worker)
    {
        var player = GetPlayer(worker.Player);
        if (player != null)
            player.FreeSettlers++;
        worker.Workplace = null;
        worker.Type = SerfType.Worker;
        ResourceWorkers.Remove(worker);
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
    /// Whether the given material is a production input that one of the
    /// player's active producer buildings is waiting for. Such materials
    /// stay at the stock flag so the carriers can deliver them.
    /// </summary>
    bool IsInputMaterialNeeded(Player player, Material material)
    {
        foreach (var building in Buildings)
        {
            if (building.Player != player.Index)
                continue;
            if (building.State != BuildingState.Active)
                continue;
            if (!building.Definition.IsProducer)
                continue;
            if (building.Definition.Consumes(material) && building.Input.Get(material) <= 0)
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
                // Leftover construction materials of finished buildings go back
                // to the player's stock.
                if (building.State != BuildingState.ConstructionSite && !building.DeliveredMaterials.IsEmpty)
                {
                    foreach (var (material, count) in building.DeliveredMaterials.NonEmpty())
                    {
                        player.Inventory.Add(material, count);
                        building.DeliveredMaterials.Remove(material, count);
                    }
                }
            }

            building.Tick();

            // A finished building gets a worker from the free settlers. The
            // worker needs the required tool in the player's stock; without
            // it the building stays unstaffed until the tool is produced.
            if (building.State == BuildingState.Active
                && !building.HasWorker
                && building.Definition.NeedsWorker
                && player != null
                && player.FreeSettlers > 0
                && HasWorkerTool(player, building))
            {
                if (building.Definition.IsResourceProducer)
                {
                    // Resource producers (lumberjack, stonecutter) are
                    // staffed by a single resource worker that gathers
                    // trees/stones (like settlers-remake).
                    AssignResourceWorker(building, player);
                }
                else
                {
                    player.FreeSettlers--;
                    ConsumeWorkerTool(building, player);
                    building.AssignWorker(building.Definition.Worker);
                }
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

        // Production inputs that arrived at the building flags are moved
        // into the building's input before the carriers can take them back
        // to the stock.
        foreach (var building in Buildings)
        {
            if (building.State != BuildingState.Active || building.Flag == null)
                continue;
            if (!building.Definition.IsProducer)
                continue;

            foreach (var input in building.Definition.Inputs)
            {
                while (building.Input.Get(input) <= 0 && building.Flag.Contains(input))
                {
                    building.Flag.TakeGood(input);
                    building.Input.Add(input);
                }
            }
        }

        // Goods that arrived at the stock flags go into the player's stock.
        // Materials still needed by a construction site or as a production
        // input stay at the flag so the carriers can deliver them.
        for (int i = 0; i < Networks.Count; i++)
        {
            var player = GetPlayer(i);
            var stock = Networks[i].StockFlag;
            if (player == null || stock == null)
                continue;

            while (stock.Count > 0)
            {
                var good = stock.Peek();
                if (good.HasValue && (IsConstructionMaterialNeeded(player, good.Value) || IsInputMaterialNeeded(player, good.Value)))
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

        // Production inputs are sent from the player's stock to the
        // building flags through the transport network; the carriers bring
        // them to the producer buildings.
        for (int i = 0; i < Networks.Count; i++)
        {
            var player = GetPlayer(i);
            var stock = Networks[i].StockFlag;
            if (player == null || stock == null)
                continue;

            foreach (var building in Buildings)
            {
                if (building.Player != i || building.State != BuildingState.Active)
                    continue;
                if (!building.Definition.IsProducer)
                    continue;

                foreach (var input in building.Definition.Inputs)
                {
                    if (building.Input.Get(input) > 0)
                        continue;
                    if (player.Inventory.Get(input) <= 0)
                        continue;

                    player.Inventory.Remove(input);
                    stock.AddGood(input);
                    break;
                }
            }
        }

        // Carriers walk along the roads and move the goods.
        foreach (var network in Networks)
            network.Tick();

        // Settlers stand around when idle (like free settlers in the real
        // game); they do not wander randomly across the map. Collectors,
        // builders and resource workers are driven by their own state
        // machines.
        foreach (var settler in Settlers)
        {
            if (settler.Type is SerfType.Collector or SerfType.Builder or SerfType.ResourceWorker)
                continue;
            settler.Tick();
        }

        // Resource collectors walk to stacks, pick up goods and carry them
        // to the player's stock.
        TickCollectors();

        // Builders build the construction sites.
        TickBuilders();

        // Resource workers cut trees and mine stones.
        TickResourceWorkers();

        // Military buildings recruit soldiers and attack enemies.
        TickMilitary();

        // The territory is recomputed from the military buildings.
        if (Tick % 30 == 0)
            RecomputeTerritory();

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
