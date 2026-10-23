/*
 * GameTests.cs - Tests for the game state and tick loop
 */

using Freeserf3.Data;
using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class GameTests : DataTestBase
{
    [TestMethod]
    public void TickAdvancesClock()
    {
        var game = new Game(new Map(8), 1234);

        Assert.AreEqual(0UL, game.Tick);

        game.TickOnce();
        game.TickOnce();
        game.TickOnce();

        Assert.AreEqual(3UL, game.Tick);
    }

    [TestMethod]
    public void TickIsDeterministic()
    {
        var a = new Game(new Map(8), 99);
        var b = new Game(new Map(8), 99);

        for (int i = 0; i < 50; i++)
        {
            a.TickOnce();
            b.TickOnce();
        }

        Assert.AreEqual(a.Tick, b.Tick);
        Assert.AreEqual(a.Random.Next(), b.Random.Next());
    }

    [TestMethod]
    public void CreatePlayersFromMap()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var mapFile = gameData.FindMapFiles().FirstOrDefault();
        Assert.IsNotNull(mapFile, "No map files found");

        var reader = gameData.LoadMap(mapFile!);
        reader.LoadMapResources();
        reader.ReadBasicMapInformation();
        reader.ReadMapData();

        var map = Map.FromReader(reader);
        var game = new Game(map, 42);
        game.CreatePlayers();

        Assert.AreEqual(reader.Players.Count, game.Players.Count);

        for (int i = 0; i < game.Players.Count; i++)
        {
            var player = game.Players[i];
            var info = reader.Players[i];

            Assert.AreEqual(i, player.Index);
            Assert.AreEqual(info.Name, player.Name);
            Assert.AreEqual(info.Nation, player.Nation);
            Assert.AreEqual(info.StartX, player.StartPosition.X);
            Assert.AreEqual(info.StartY, player.StartPosition.Y);
        }
    }

    [TestMethod]
    public void PlayerBuildingCounts()
    {
        var player = new Player(0, "Test");
        player.BuildingCounts = new int[10];

        player.AddBuilding(2);
        player.AddBuilding(2);
        player.AddBuilding(5);

        Assert.AreEqual(2, player.GetBuildingCount(2));
        Assert.AreEqual(1, player.GetBuildingCount(5));
        Assert.AreEqual(3, player.TotalBuildings);

        player.RemoveBuilding(2);
        Assert.AreEqual(1, player.GetBuildingCount(2));
        Assert.AreEqual(2, player.TotalBuildings);

        // Removing below zero is a no-op.
        player.RemoveBuilding(2);
        player.RemoveBuilding(2);
        Assert.AreEqual(0, player.GetBuildingCount(2));
    }

    [TestMethod]
    public void PlayerPopulationAndFlags()
    {
        var player = new Player(0, "Test")
        {
            Population = 10,
            FreeSettlers = 3,
            IsAI = true,
            IsActive = true
        };

        Assert.AreEqual(10, player.Population);
        Assert.AreEqual(3, player.FreeSettlers);
        Assert.IsTrue(player.IsAI);
        Assert.IsTrue(player.IsActive);
    }

    [TestMethod]
    public void StartGameGivesStartingResources()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        var game = new Game(map, 42);
        game.StartGame();

        var player = game.GetPlayer(0);
        Assert.IsNotNull(player);
        Assert.AreEqual(10, player!.Inventory.Get(Material.Plank));
        Assert.AreEqual(10, player.Inventory.Get(Material.Stone));
    }

    [TestMethod]
    public void StartGameSetsPopulationFromMapSettlers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 2 });
        map.Settlers.Add(new MapSettler { Party = 1, PosX = 3, PosY = 3 });

        var game = new Game(map, 42);
        game.StartGame();

        Assert.AreEqual(2, game.GetPlayer(0)!.Population);
        Assert.AreEqual(2, game.GetPlayer(0)!.FreeSettlers);
        Assert.AreEqual(1, game.GetPlayer(1)!.Population);
    }

    [TestMethod]
    public void ConstructionConsumesMaterialsAndBecomesActive()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Lumberjack costs 2 stone + 2 plank.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        Assert.AreEqual(BuildingState.ConstructionSite, building.State);

        // A builder is assigned from the free settlers.
        Assert.AreEqual(1, game.Builders.Count);
        Assert.AreEqual(SerfType.Builder, game.Builders[0].Type);

        // The materials are delivered by the carrier and built by the
        // builder over time.
        for (int i = 0; i < 500; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.AreEqual(100, building.ConstructionProgress);
        Assert.AreEqual(0, game.Builders.Count);
        // 2 stone + 2 plank consumed from the starting stock (10 each).
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Stone));
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void ConstructionWaitsForFreeSettler()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // No free settlers: no builder is assigned and nothing is built.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 100; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.ConstructionSite, building.State);
        Assert.AreEqual(0, game.Builders.Count);
    }

    [TestMethod]
    public void WorkerAssignedFromFreeSettlers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 500; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);
        Assert.AreEqual(0, game.GetPlayer(0)!.FreeSettlers);
    }

    [TestMethod]
    public void ProductionTransfersToPlayerInventory()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Sawmill: 3 plank + 4 stone, trunk -> plank.
        var building = game.PlaceBuilding(BuildingType.Sawmill, new Position(4, 4), 0);
        for (int i = 0; i < 1000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);

        // Give the sawmill a trunk and let it produce.
        building.Input.Add(Material.Trunk);
        int productionTime = building.Definition.ProductionTime;
        // Production plus time for the carrier to deliver the plank to the
        // player's stock.
        for (int i = 0; i < productionTime + 100; i++)
            game.TickOnce();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
        Assert.AreEqual(0, building.Flag!.Count);
        // 7 plank left after construction (10 - 3) plus 1 produced.
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    /// <summary>Sets all cells of the map to grass so roads can be built.</summary>
    static void SetGrass(Map map)
    {
        for (int y = 0; y < map.Size; y++)
        {
            for (int x = 0; x < map.Size; x++)
            {
                var cell = map.GetCell(x, y);
                cell.Landscape = 16; // Grass
                map.SetCell(x, y, cell);
            }
        }
    }

    [TestMethod]
    public void IsWalkableRejectsRivers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Grass is walkable.
        Assert.IsTrue(game.IsWalkable(map, new Position(1, 1)));

        // Rivers are not walkable (carriers must not walk over water).
        var cell = map.GetCell(2, 2);
        cell.Landscape = 96; // River1
        map.SetCell(2, 2, cell);
        Assert.IsFalse(game.IsWalkable(map, new Position(2, 2)));

        cell.Landscape = 99; // River4
        map.SetCell(2, 2, cell);
        Assert.IsFalse(game.IsWalkable(map, new Position(2, 2)));
    }

    [TestMethod]
    public void SerfFacingFollowsMovementDirection()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var serf = new Serf(0, new Position(2, 2), SerfType.Carrier);
        // Walk east: (2,2) -> (3,2) -> (4,2).
        serf.SetPath(new List<Position> { new(3, 2), new(4, 2) });
        serf.Tick();
        Assert.AreEqual(Direction.East, serf.Facing);
        Assert.AreEqual(new Position(3, 2), serf.Position);

        // Walk north-west: (3,2) -> (2,1).
        serf.SetPath(new List<Position> { new(2, 1) });
        serf.Tick();
        Assert.AreEqual(Direction.NorthWest, serf.Facing);
        Assert.AreEqual(new Position(2, 1), serf.Position);
    }

    [TestMethod]
    public void IsWalkableRejectsBuildings()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Grass is walkable.
        Assert.IsTrue(game.IsWalkable(map, new Position(4, 4)));

        // Place a building; its 3x3 footprint blocks movement.
        game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        Assert.IsFalse(game.IsWalkable(map, new Position(4, 4)));
        Assert.IsFalse(game.IsWalkable(map, new Position(3, 3)));
        Assert.IsFalse(game.IsWalkable(map, new Position(5, 5)));
        // Outside the footprint it is walkable again.
        Assert.IsTrue(game.IsWalkable(map, new Position(2, 2)));
        Assert.IsTrue(game.IsWalkable(map, new Position(6, 6)));
    }

    [TestMethod]
    public void RoadPathAvoidsBuildings()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // A building in the middle of the map.
        game.PlaceBuilding(BuildingType.Lumberjack, new Position(8, 8), 0);

        // A road from (2,2) to (14,14) must not pass through the building.
        var network = game.Networks[0];
        var start = network.AddFlag(new Position(2, 2), 0);
        var end = network.AddFlag(new Position(14, 14), 0);
        var road = network.Connect(start, end, 0, game.IsWalkable);

        Assert.IsNotNull(road);
        foreach (var cell in road!.Path)
        {
            Assert.IsTrue(game.IsWalkable(map, cell), $"Road passes through blocked cell {cell}");
        }
    }

    [TestMethod]
    public void PlaceBuildingCreatesCarrierRoad()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        var network = game.Networks[0];

        Assert.IsNotNull(building.Flag);
        Assert.AreEqual(1, network.Roads.Count);
        Assert.AreEqual(1, game.Carriers.Count());
        Assert.AreEqual(SerfType.Carrier, game.Carriers.First().Type);
    }

    [TestMethod]
    public void StartGameCreatesWalkingSettlers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 2 });
        map.Settlers.Add(new MapSettler { Party = 1, PosX = 3, PosY = 3 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        Assert.AreEqual(3, game.Settlers.Count);
        Assert.AreEqual(new Position(1, 1), game.Settlers[0].Position);
        Assert.AreEqual(0, game.Settlers[0].Player);
        Assert.AreEqual(1, game.Settlers[2].Player);
    }

    [TestMethod]
    public void StartGameCreatesResourceStacks()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Stacks.Add(new MapStack { PosX = 2, PosY = 2, StackType = 1, Count = 6 }); // Plank
        map.Stacks.Add(new MapStack { PosX = 3, PosY = 3, StackType = 2, Count = 4 }); // Stone
        map.Stacks.Add(new MapStack { PosX = 4, PosY = 4, StackType = 99, Count = 5 }); // Unknown
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        Assert.AreEqual(2, game.Stacks.Count);
        Assert.AreEqual(Material.Plank, game.Stacks[0].Material);
        Assert.AreEqual(6, game.Stacks[0].Count);
        Assert.AreEqual(Material.Stone, game.Stacks[1].Material);
    }

    [TestMethod]
    public void CollectorPicksUpAndCarriesToStock()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Stacks.Add(new MapStack { PosX = 9, PosY = 8, StackType = 1, Count = 6 }); // Plank
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // The first settler is a collector.
        var collector = game.Collectors[0];
        Assert.AreEqual(SerfType.Collector, collector.Type);
        Assert.AreEqual(6, game.Stacks[0].Count); // stack not yet touched

        // Let the collector walk to the stack, pick up and carry to stock.
        for (int i = 0; i < 200; i++)
            game.TickOnce();

        // The stack lost goods and the player's stock gained them.
        Assert.IsTrue(game.Stacks[0].Count < 6);
        Assert.AreEqual(10 + (6 - game.Stacks[0].Count), game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void SettlersWanderWhenIdle()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 9, PosY = 8 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // The second settler is a wanderer (the first is a collector).
        var settler = game.Settlers[1];
        Assert.AreEqual(MovableState.Idle, settler.State);

        // After the idle pause the settler picks a walk target.
        for (int i = 0; i < 40; i++)
            game.TickOnce();

        Assert.AreEqual(MovableState.Walking, settler.State);
        Assert.IsNotNull(settler.Path);
        Assert.IsTrue(settler.Path!.Count > 0);

        // Walking advances the position.
        var start = settler.Position;
        for (int i = 0; i < 20; i++)
            game.TickOnce();
        Assert.AreNotEqual(start, settler.Position);
    }

    [TestMethod]
    public void LivingHouseGrowsPopulation()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0);
        for (int i = 0; i < 500; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, house.State);
        int before = game.GetPlayer(0)!.Population;

        // Population grows every 600 ticks.
        for (int i = 0; i < 600; i++)
            game.TickOnce();

        Assert.AreEqual(before + 1, game.GetPlayer(0)!.Population);
        Assert.AreEqual(before + 1, game.GetPlayer(0)!.FreeSettlers);
    }

    [TestMethod]
    public void WinConditionReached()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();
        game.TargetPopulation = 5;

        Assert.IsFalse(game.IsWon);

        // Build enough living houses to grow the population to the target.
        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0);
        for (int i = 0; i < 500; i++)
            game.TickOnce();

        for (int i = 0; i < 600 * 5; i++)
            game.TickOnce();

        Assert.IsTrue(game.IsWon);
    }

    [TestMethod]
    public void StartGameSetsWinTargetAboveStartingPopulation()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 2 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 3 });

        var game = new Game(map, 42);
        game.StartGame();

        // Target is starting population (3) plus the growth goal (20).
        Assert.AreEqual(23, game.TargetPopulation);
        Assert.IsFalse(game.IsWon);
    }
}
