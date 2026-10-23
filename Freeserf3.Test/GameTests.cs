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
        var game = new Game(map, 42);
        game.StartGame();

        // Lumberjack costs 2 stone + 2 plank.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(1, 1), 0);
        Assert.AreEqual(BuildingState.ConstructionSite, building.State);

        // Each tick delivers one material from the player's stock.
        for (int i = 0; i < 4; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.AreEqual(100, building.ConstructionProgress);
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Stone));
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void WorkerAssignedFromFreeSettlers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(1, 1), 0);
        for (int i = 0; i < 4; i++)
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
        var game = new Game(map, 42);
        game.StartGame();

        // Sawmill: 3 plank + 4 stone, trunk -> plank.
        var building = game.PlaceBuilding(BuildingType.Sawmill, new Position(1, 1), 0);
        for (int i = 0; i < 7; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);

        // Give the sawmill a trunk and let it produce.
        building.Input.Add(Material.Trunk);
        int productionTime = building.Definition.ProductionTime;
        for (int i = 0; i < productionTime; i++)
            game.TickOnce();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
        // 7 plank left after construction (10 - 3) plus 1 produced.
        Assert.AreEqual(8, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void LivingHouseGrowsPopulation()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        var game = new Game(map, 42);
        game.StartGame();

        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(1, 1), 0);
        int cost = house.Definition.TotalConstructionCost;
        for (int i = 0; i < cost; i++)
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
        var game = new Game(map, 42);
        game.StartGame();
        game.TargetPopulation = 5;

        Assert.IsFalse(game.IsWon);

        // Build enough living houses to grow the population to the target.
        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(1, 1), 0);
        int cost = house.Definition.TotalConstructionCost;
        for (int i = 0; i < cost; i++)
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
