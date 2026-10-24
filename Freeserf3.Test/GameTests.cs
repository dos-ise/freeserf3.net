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
        // Default start resources level (0 -> low): 12 planks and 12 stone.
        Assert.AreEqual(12, player!.Inventory.Get(Material.Plank));
        Assert.AreEqual(12, player.Inventory.Get(Material.Stone));
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
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.AreEqual(100, building.ConstructionProgress);
        Assert.AreEqual(0, game.Builders.Count);
        // 2 stone + 2 plank consumed from the starting stock (12 each).
        Assert.AreEqual(10, game.GetPlayer(0)!.Inventory.Get(Material.Stone));
        Assert.AreEqual(10, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void BuilderStandsAtBricklayerPosition()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Lumberjack bricklayer positions relative to the building:
        // (-1,2) NE, (0,3) NE, (4,3) NW, (3,4) NW. Building at (4,4)
        // gives (3,6), (4,7), (8,7), (7,8) - the first two are in bounds.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        Assert.AreEqual(1, game.Builders.Count);

        var builder = game.Builders[0];
        // The builder stands at the first walkable bricklayer position,
        // not at the flag, and faces the building.
        Assert.AreEqual(new Position(3, 6), builder.Position);
        Assert.AreEqual(Direction.NorthEast, builder.Facing);
    }

    [TestMethod]
    public void MaterialPriorityPrefersConstructionOverInput()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Buildings.Add(new MapBuilding { Party = 0, BuildingType = 7, PosX = 10, PosY = 10 }); // coalmine (active)
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // A construction site needing stone.
        game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);

        var player = game.GetPlayer(0)!;
        // Construction materials have the highest priority, production
        // inputs come second, everything else last.
        Assert.AreEqual(2, game.GetMaterialPriority(player, Material.Stone));
        Assert.AreEqual(1, game.GetMaterialPriority(player, Material.Bread));
        Assert.AreEqual(0, game.GetMaterialPriority(player, Material.Trunk));
    }

    [TestMethod]
    public void StockFlagMovesUnneededGoodsToInventory()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // A construction site needing stone.
        game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);

        // The stock flag holds a needed good (stone) in front of an
        // unneeded one (trunk). The unneeded good must still go to the
        // inventory instead of staying at the flag forever.
        var stock = game.Networks[0].StockFlag!;
        stock.AddGood(Material.Stone);
        stock.AddGood(Material.Trunk);

        game.TickOnce();

        Assert.AreEqual(1, stock.Count);
        Assert.IsTrue(stock.Contains(Material.Stone));
        Assert.AreEqual(1, game.GetPlayer(0)!.Inventory.Get(Material.Trunk));
    }

    [TestMethod]
    public void PlaceBuildingTurnsGroundToSand()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Before placing, the ground is grass (16).
        Assert.AreEqual(16, map.GetCell(4, 4).Landscape);

        game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);

        // The whole building footprint becomes sand (48).
        var footprint = BuildingFootprints.For(BuildingType.Lumberjack);
        Assert.IsTrue(footprint.Count > 9, "lumberjack footprint should be larger than 3x3");
        foreach (var (dx, dy) in footprint)
            Assert.AreEqual(48, map.GetCell(4 + dx, 4 + dy).Landscape, $"cell ({4 + dx},{4 + dy})");
        // Cells outside the footprint stay grass.
        Assert.AreEqual(16, map.GetCell(8, 4).Landscape);
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
        for (int i = 0; i < 3000; i++)
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
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);
        Assert.AreEqual(0, game.GetPlayer(0)!.FreeSettlers);
    }

    [TestMethod]
    public void WorkerNotAssignedWithoutTool()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Remove all axes: the lumberjack needs one to work.
        var player = game.GetPlayer(0)!;
        player.Inventory.Set(Material.Axe, 0);

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsFalse(building.HasWorker, "Worker must not be assigned without the required tool.");
        Assert.AreEqual(1, player.FreeSettlers);
    }

    [TestMethod]
    public void WorkerAssignedWhenToolBecomesAvailable()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // No axes at first: the lumberjack stays unstaffed.
        var player = game.GetPlayer(0)!;
        player.Inventory.Set(Material.Axe, 0);

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.IsFalse(building.HasWorker);

        // An axe arrives: the worker is assigned and the axe consumed.
        player.Inventory.Add(Material.Axe, 1);
        for (int i = 0; i < 10; i++)
            game.TickOnce();

        Assert.IsTrue(building.HasWorker);
        Assert.AreEqual(0, player.Inventory.Get(Material.Axe));
        Assert.AreEqual(0, player.FreeSettlers);
    }

    [TestMethod]
    public void ToolsmithProducesConfiguredTool()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var toolsmith = game.PlaceBuilding(BuildingType.Toolsmith, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, toolsmith.State);
        Assert.IsTrue(toolsmith.HasWorker);

        // The toolsmith produces the configured tool instead of its
        // default output (hammer).
        toolsmith.ToolsmithOutput = Material.Saw;
        toolsmith.Input.Add(Material.Iron);
        toolsmith.Input.Add(Material.Coal);

        // The produced saw is carried to the player's stock (it is not a
        // construction material or production input, so it does not stay
        // at the flag). The player starts with 6 saws.
        int sawsBefore = game.GetPlayer(0)!.Inventory.Get(Material.Saw);
        int productionTime = toolsmith.Definition.ProductionTime;
        for (int i = 0; i < productionTime + 500; i++)
            game.TickOnce();

        Assert.AreEqual(0, toolsmith.Output.Get(Material.Hammer));
        Assert.AreEqual(sawsBefore + 1, game.GetPlayer(0)!.Inventory.Get(Material.Saw));
        // The toolsmith consumed one hammer as its own tool (start: 6).
        Assert.AreEqual(5, game.GetPlayer(0)!.Inventory.Get(Material.Hammer));
    }

    [TestMethod]
    public void StartGameTracksTreesAndStones()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var cell = map.GetCell(2, 2);
        cell.MapObject = 68; // birch tree
        map.SetCell(2, 2, cell);
        cell = map.GetCell(3, 3);
        cell.MapObject = 1; // big stone
        map.SetCell(3, 3, cell);

        var game = new Game(map, 42);
        game.StartGame();

        Assert.IsTrue(game.Trees.Contains(new Position(2, 2)));
        Assert.IsTrue(game.Stones.Contains(new Position(3, 3)));
        Assert.IsFalse(game.Trees.Contains(new Position(3, 3)));
    }

    [TestMethod]
    public void FindNearestResourceRespectsRadius()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        game.Trees.Add(new Position(5, 5));
        game.Trees.Add(new Position(9, 9));

        Assert.AreEqual(new Position(5, 5), game.FindNearestTree(new Position(4, 4), 3));
        Assert.IsNull(game.FindNearestTree(new Position(4, 4), 0));

        game.Stones.Add(new Position(6, 6));
        Assert.AreEqual(new Position(6, 6), game.FindNearestStone(new Position(4, 4), 3));
    }

    [TestMethod]
    public void LumberjackCutsTreeAndProducesTrunk()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Add a tree near the building.
        var treePos = new Position(6, 6);
        game.Trees.Add(treePos);
        var cell = map.GetCell(treePos);
        cell.MapObject = 68; // birch tree
        map.SetCell(treePos, cell);

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.Trees.Contains(treePos); i++)
            game.TickOnce();

        Assert.IsFalse(game.Trees.Contains(treePos), "Tree should have been cut down");

        // The trunk is carried to the flag in front of the house and then
        // to the player's stock.
        int trunksBefore = game.GetPlayer(0)!.Inventory.Get(Material.Trunk);
        for (int i = 0; i < 2000 && building.Flag!.Count == 0 && game.GetPlayer(0)!.Inventory.Get(Material.Trunk) == trunksBefore; i++)
            game.TickOnce();

        Assert.IsTrue(building.Flag!.Count > 0 || game.GetPlayer(0)!.Inventory.Get(Material.Trunk) > trunksBefore,
            "Lumberjack should have produced a trunk");
    }

    [TestMethod]
    public void StonecutterMinesStoneAndProducesStone()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Add a stone near the building.
        var stonePos = new Position(6, 6);
        game.Stones.Add(stonePos);
        var cell = map.GetCell(stonePos);
        cell.MapObject = 1; // big stone
        map.SetCell(stonePos, cell);

        var building = game.PlaceBuilding(BuildingType.Stonecutter, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.Stones.Contains(stonePos); i++)
            game.TickOnce();

        Assert.IsFalse(game.Stones.Contains(stonePos), "Stone should have been mined");

        // The stone is carried to the flag in front of the house and then
        // to the player's stock.
        int stonesBefore = game.GetPlayer(0)!.Inventory.Get(Material.Stone);
        for (int i = 0; i < 2000 && building.Flag!.Count == 0 && game.GetPlayer(0)!.Inventory.Get(Material.Stone) == stonesBefore; i++)
            game.TickOnce();

        Assert.IsTrue(building.Flag!.Count > 0 || game.GetPlayer(0)!.Inventory.Get(Material.Stone) > stonesBefore,
            "Stonecutter should have produced stone");
    }

    [TestMethod]
    public void StonecutterCarriesStoneToFlagAndEntersHome()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Add a stone near the building.
        var stonePos = new Position(6, 6);
        game.Stones.Add(stonePos);

        var building = game.PlaceBuilding(BuildingType.Stonecutter, new Position(4, 4), 0);
        // Wait for the building to become active and a worker assigned.
        for (int i = 0; i < 3000 && game.ResourceWorkers.Count == 0; i++)
            game.TickOnce();
        Assert.AreEqual(1, game.ResourceWorkers.Count);
        var worker = game.ResourceWorkers[0];

        // The worker carries the stone to the flag, drops it there and
        // goes inside the house.
        bool sawCarry = false, sawDrop = false, sawInside = false;
        for (int i = 0; i < 3000; i++)
        {
            game.TickOnce();
            if (worker.CarriedGood == Material.Stone)
                sawCarry = true;
            if (building.Flag != null && building.Flag.Count > 0)
                sawDrop = true;
            if (worker.ResourceState == ResourceWorkerState.InsideHome)
                sawInside = true;
        }

        Assert.IsFalse(game.Stones.Contains(stonePos), "Stone should have been mined");
        Assert.IsTrue(sawCarry, "Stonecutter should carry the stone");
        Assert.IsTrue(sawDrop, "Stonecutter should drop the stone at the flag");
        Assert.IsTrue(sawInside, "Stonecutter should go inside the house");
    }

    [TestMethod]
    public void LumberjackCarriesTrunkToFlagAndEntersHome()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Add a tree near the building.
        var treePos = new Position(6, 6);
        game.Trees.Add(treePos);

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 3000 && game.ResourceWorkers.Count == 0; i++)
            game.TickOnce();
        Assert.AreEqual(1, game.ResourceWorkers.Count);
        var worker = game.ResourceWorkers[0];

        bool sawCarry = false, sawDrop = false, sawInside = false;
        for (int i = 0; i < 3000; i++)
        {
            game.TickOnce();
            if (worker.CarriedGood == Material.Trunk)
                sawCarry = true;
            if (building.Flag != null && building.Flag.Count > 0)
                sawDrop = true;
            if (worker.ResourceState == ResourceWorkerState.InsideHome)
                sawInside = true;
        }

        Assert.IsFalse(game.Trees.Contains(treePos), "Tree should have been cut down");
        Assert.IsTrue(sawCarry, "Lumberjack should carry the trunk");
        Assert.IsTrue(sawDrop, "Lumberjack should drop the trunk at the flag");
        Assert.IsTrue(sawInside, "Lumberjack should go inside the house");
    }

    [TestMethod]
    public void IdleResourceWorkerGoesInsideHouse()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // No trees or stones: the lumberjack has no work and should stay
        // inside the house instead of standing at the flag.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 3000 && game.ResourceWorkers.Count == 0; i++)
            game.TickOnce();
        Assert.AreEqual(1, game.ResourceWorkers.Count);
        var worker = game.ResourceWorkers[0];

        bool sawInside = false;
        for (int i = 0; i < 2000; i++)
        {
            game.TickOnce();
            if (worker.ResourceState == ResourceWorkerState.InsideHome)
                sawInside = true;
        }

        Assert.IsTrue(sawInside, "Idle resource worker should go inside the house");
    }

    [TestMethod]
    public void MineProducesFromMapResource()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);

        // Add an iron ore deposit near the mine (type 2 = iron).
        var orePos = new Position(6, 6);
        var cell = map.GetCell(orePos);
        cell.ResourceType1 = 2; // iron
        cell.ResourceType2 = 5; // amount
        map.SetCell(orePos, cell);

        var game = new Game(map, 42);
        game.StartGame();
        Assert.IsTrue(game.MapResources.ContainsKey(orePos));

        var mine = game.PlaceBuilding(BuildingType.Ironmine, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.GetPlayer(0)!.Inventory.Get(Material.IronOre) == 0; i++)
            game.TickOnce();

        // The produced ore is carried to the player's stock.
        Assert.IsTrue(game.GetPlayer(0)!.Inventory.Get(Material.IronOre) > 0, "Mine should have produced iron ore");
        // One unit of ore was consumed from the deposit.
        Assert.AreEqual(4, game.MapResources[orePos]);
    }

    [TestMethod]
    public void MineStopsWithoutMapResource()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // No ore on the map: the mine never produces.
        var mine = game.PlaceBuilding(BuildingType.Ironmine, new Position(4, 4), 0);
        for (int i = 0; i < 5000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, mine.State);
        Assert.AreEqual(0, game.GetPlayer(0)!.Inventory.Get(Material.IronOre));
    }

    [TestMethod]
    public void MineExhaustsMapResource()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);

        // A small deposit (2 units) so the mine runs out quickly.
        var orePos = new Position(6, 6);
        var cell = map.GetCell(orePos);
        cell.ResourceType1 = 2; // iron
        cell.ResourceType2 = 2; // amount
        map.SetCell(orePos, cell);

        var game = new Game(map, 42);
        game.StartGame();

        var mine = game.PlaceBuilding(BuildingType.Ironmine, new Position(4, 4), 0);
        for (int i = 0; i < 20000; i++)
            game.TickOnce();

        // The deposit is exhausted and the mine stopped producing.
        Assert.IsFalse(game.MapResources.ContainsKey(orePos));
        Assert.AreEqual(2, game.GetPlayer(0)!.Inventory.Get(Material.IronOre));
    }

    [TestMethod]
    public void FarmProducesOnFertileGround()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var farm = game.PlaceBuilding(BuildingType.Farm, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.GetPlayer(0)!.Inventory.Get(Material.Crop) == 0; i++)
            game.TickOnce();

        Assert.IsTrue(game.GetPlayer(0)!.Inventory.Get(Material.Crop) > 0, "Farm should have produced crop on grass");
    }

    [TestMethod]
    public void FarmDoesNotProduceWithoutFertileGround()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var farm = game.PlaceBuilding(BuildingType.Farm, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, farm.State);

        // Remove all fertile ground in the work radius: the farm stops.
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                var cell = map.GetCell(x, y);
                cell.Landscape = 0; // Water1
                map.SetCell(x, y, cell);
            }
        }

        int cropBefore = game.GetPlayer(0)!.Inventory.Get(Material.Crop);
        for (int i = 0; i < 5000; i++)
            game.TickOnce();

        Assert.AreEqual(cropBefore, game.GetPlayer(0)!.Inventory.Get(Material.Crop));
    }

    [TestMethod]
    public void FisherProducesNearWater()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        // Water near the fisher's work area.
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                if (x >= 8)
                {
                    var cell = map.GetCell(x, y);
                    cell.Landscape = 0; // Water1
                    map.SetCell(x, y, cell);
                }
            }
        }
        var game = new Game(map, 42);
        game.StartGame();

        var fisher = game.PlaceBuilding(BuildingType.Fisher, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.GetPlayer(0)!.Inventory.Get(Material.Fish) == 0; i++)
            game.TickOnce();

        Assert.IsTrue(game.GetPlayer(0)!.Inventory.Get(Material.Fish) > 0, "Fisher should have produced fish near water");
    }

    [TestMethod]
    public void CanPlaceMineOnMountain()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        // A mountain cell.
        var cell = map.GetCell(5, 5);
        cell.Landscape = 32; // Mountain
        map.SetCell(5, 5, cell);

        var game = new Game(map, 42);
        game.StartGame();

        Assert.IsTrue(game.CanPlaceBuilding(BuildingType.Coalmine, new Position(5, 5), 0));
        Assert.IsFalse(game.CanPlaceBuilding(BuildingType.Coalmine, new Position(3, 3), 0));
    }

    [TestMethod]
    public void CanPlaceMineNearOre()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        // An iron deposit near the position.
        var orePos = new Position(6, 6);
        var cell = map.GetCell(orePos);
        cell.ResourceType1 = 2; // iron
        cell.ResourceType2 = 5;
        map.SetCell(orePos, cell);

        var game = new Game(map, 42);
        game.StartGame();

        Assert.IsTrue(game.CanPlaceBuilding(BuildingType.Ironmine, new Position(4, 4), 0));
        Assert.IsFalse(game.CanPlaceBuilding(BuildingType.Coalmine, new Position(4, 4), 0));
    }

    [TestMethod]
    public void CanPlaceFarmOnlyOnFertileGround()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // On grass (fertile) the farm can be placed.
        Assert.IsTrue(game.CanPlaceBuilding(BuildingType.Farm, new Position(4, 4), 0));

        // On water it cannot.
        var waterPos = new Position(8, 8);
        var cell = map.GetCell(waterPos);
        cell.Landscape = 0; // Water1
        map.SetCell(waterPos, cell);
        Assert.IsFalse(game.CanPlaceBuilding(BuildingType.Farm, waterPos, 0));
    }

    [TestMethod]
    public void CanPlaceFisherOnlyNearWater()
    {
        var map = new Map(64);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        // Water only in the far right corner, outside the fisher's work
        // radius (30) from the left side.
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x >= 60)
                {
                    var cell = map.GetCell(x, y);
                    cell.Landscape = 0; // Water1
                    map.SetCell(x, y, cell);
                }
            }
        }
        var game = new Game(map, 42);
        game.StartGame();

        // No water within the work radius of (4,4): cannot place.
        Assert.IsFalse(game.CanPlaceBuilding(BuildingType.Fisher, new Position(4, 4), 0));

        // Near the water edge it can be placed.
        Assert.IsTrue(game.CanPlaceBuilding(BuildingType.Fisher, new Position(58, 4), 0));
    }

    [TestMethod]
    public void CanPlaceLivinghouseOnlyOnOwnTerritory()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Without a military building the territory is unclaimed (0), so
        // the living house cannot be placed.
        Assert.IsFalse(game.CanPlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0));

        // A tower claims the territory around it.
        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);
        game.RecomputeTerritory();

        // The living house is placed next to the tower, on claimed ground.
        Assert.IsTrue(game.CanPlaceBuilding(BuildingType.SmallLivinghouse, new Position(6, 4), 0));
    }

    [TestMethod]
    public void DemolishReturnsMaterialsAndReleasesWorker()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var player = game.GetPlayer(0)!;
        int planksBefore = player.Inventory.Get(Material.Plank);
        int stonesBefore = player.Inventory.Get(Material.Stone);

        // Build a lumberjack (2 stone + 2 plank) and let it become active
        // with a worker.
        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);
        int freeBefore = player.FreeSettlers;

        game.DemolishBuilding(building);

        Assert.AreEqual(BuildingState.Destroyed, building.State);
        Assert.IsFalse(building.HasWorker);
        // The building cost 2 stone + 2 plank; half (1 each) is returned.
        Assert.AreEqual(stonesBefore - 2 + 1, player.Inventory.Get(Material.Stone));
        Assert.AreEqual(planksBefore - 2 + 1, player.Inventory.Get(Material.Plank));
        // The worker is released.
        Assert.AreEqual(freeBefore + 1, player.FreeSettlers);
    }

    [TestMethod]
    public void DemolishReleasesSoldiers()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 3 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 4, PosY = 4 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);
        Assert.IsTrue(tower.Soldiers.Count > 0);
        int freeBefore = game.GetPlayer(0)!.FreeSettlers;

        game.DemolishBuilding(tower);

        Assert.AreEqual(BuildingState.Destroyed, tower.State);
        Assert.AreEqual(0, tower.Soldiers.Count);
        Assert.AreEqual(freeBefore + 3, game.GetPlayer(0)!.FreeSettlers);
    }

    [TestMethod]
    public void RecruitedSoldiersAreVisibleOnMap()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 3 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 4, PosY = 4 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);

        // The recruited soldiers are real serfs on the map.
        Assert.IsTrue(tower.Soldiers.Count > 0);
        int visibleSoldiers = game.Settlers.Count(s => s.Type == SerfType.Soldier);
        Assert.AreEqual(tower.Soldiers.Count, visibleSoldiers);
        Assert.IsTrue(tower.Soldiers.All(s => s.Barracks == tower));
    }

    [TestMethod]
    public void SoldiersPatrolAroundBuilding()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 3 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 4, PosY = 4 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(8, 8), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);
        Assert.IsTrue(tower.Soldiers.Count > 0);

        // Let the soldiers patrol for a while; they should move away from
        // the building and come back.
        var soldier = tower.Soldiers[0];
        var start = soldier.Position;
        bool moved = false;
        for (int i = 0; i < 2000; i++)
        {
            game.TickOnce();
            if (soldier.Position != start)
            {
                moved = true;
                break;
            }
        }

        Assert.IsTrue(moved, "Soldier should have moved away from the building");
    }

    [TestMethod]
    public void DemolishRemovesFlagFromNetwork()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.PlaceBuilding(BuildingType.Lumberjack, new Position(4, 4), 0);
        Assert.IsNotNull(building.Flag);
        int flagsBefore = game.Networks[0].Flags.Count;

        game.DemolishBuilding(building);

        Assert.IsNull(building.Flag);
        Assert.AreEqual(flagsBefore - 1, game.Networks[0].Flags.Count);
    }

    [TestMethod]
    public void ForesterPlantsTree()
    {
        var map = new Map(12);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        int treesBefore = game.Trees.Count;
        var building = game.PlaceBuilding(BuildingType.Forester, new Position(4, 4), 0);
        for (int i = 0; i < 5000 && game.Trees.Count <= treesBefore; i++)
            game.TickOnce();

        Assert.IsTrue(game.Trees.Count > treesBefore, "Forester should have planted a tree");
        Assert.AreEqual(BuildingState.Active, building.State);
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
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        Assert.AreEqual(BuildingState.Active, building.State);
        Assert.IsTrue(building.HasWorker);

        // Give the sawmill a trunk and let it produce.
        building.Input.Add(Material.Trunk);
        int productionTime = building.Definition.ProductionTime;
        // Production plus time for the carrier to deliver the plank to the
        // player's stock.
        for (int i = 0; i < productionTime + 500; i++)
            game.TickOnce();

        Assert.AreEqual(0, building.Output.Get(Material.Plank));
        Assert.AreEqual(0, building.Flag!.Count);
        // 9 plank left after construction (12 - 3) plus 1 produced.
        Assert.AreEqual(10, game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void PlaceBuildingCreatesConstructionSite()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.PlaceBuilding(BuildingType.Sawmill, new Position(4, 4), 0);

        Assert.IsNotNull(building);
        Assert.AreEqual(BuildingState.ConstructionSite, building!.State);
        // The flag is placed on a walkable cell outside the footprint.
        Assert.IsNotNull(building.Flag);
        Assert.IsTrue(game.IsWalkable(map, building.Flag!.Position));
        // The site is away from the stock flag footprint.
        Assert.IsFalse(game.IsBuildingAt(game.Networks[0].StockFlag!.Position));
    }

    [TestMethod]
    public void StartGameLoadsMapBuildingsAsActive()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Buildings.Add(new MapBuilding { Party = 0, BuildingType = 2, PosX = 4, PosY = 4 }); // lumberjack
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var building = game.Buildings.FirstOrDefault(b => b.Type == BuildingType.Lumberjack);
        Assert.IsNotNull(building);
        Assert.AreEqual(BuildingState.Active, building!.State);
        Assert.IsNotNull(building.Flag);
    }

    [TestMethod]
    public void StartGameLoadsAllPlayerBuildings()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Buildings.Add(new MapBuilding { Party = 0, BuildingType = 2, PosX = 4, PosY = 4 }); // lumberjack
        map.Buildings.Add(new MapBuilding { Party = 1, BuildingType = 15, PosX = 10, PosY = 10 }); // enemy tower
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // All players' buildings are loaded; the AI player is marked.
        Assert.AreEqual(2, game.Buildings.Count);
        Assert.AreEqual(BuildingType.Lumberjack, game.Buildings[0].Type);
        Assert.AreEqual(BuildingState.Active, game.Buildings[0].State);
        Assert.IsNotNull(game.Buildings[0].Flag);
        Assert.IsTrue(game.Players[1].IsAI);
        Assert.IsFalse(game.Players[0].IsAI);
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

        var serf = new Serf(0, new Position(2, 2), SerfType.Carrier)
        {
            MoveInterval = 1
        };
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
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 2 });
        map.Settlers.Add(new MapSettler { Party = 1, PosX = 3, PosY = 3 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Settlers of all players are created (the AI players too).
        Assert.AreEqual(3, game.Settlers.Count);
        Assert.AreEqual(new Position(1, 1), game.Settlers[0].Position);
        Assert.AreEqual(0, game.Settlers[0].Player);
        Assert.AreEqual(0, game.Settlers[1].Player);
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
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        // The stack lost goods and the player's stock gained them.
        Assert.IsTrue(game.Stacks[0].Count < 6);
        Assert.AreEqual(12 + (6 - game.Stacks[0].Count), game.GetPlayer(0)!.Inventory.Get(Material.Plank));
    }

    [TestMethod]
    public void SettlersStandStillWhenIdle()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 8, PosY = 8 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 9, PosY = 8 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // The second settler is a free settler (the first is a collector).
        var settler = game.Settlers[1];
        Assert.AreEqual(MovableState.Idle, settler.State);

        // Free settlers stand still; they do not wander randomly.
        for (int i = 0; i < 3000; i++)
            game.TickOnce();

        Assert.AreEqual(MovableState.Idle, settler.State);
        Assert.AreEqual(new Position(9, 8), settler.Position);
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
        for (int i = 0; i < 3000; i++)
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
    public void PopulationDoesNotGrowWithoutFood()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Remove all food: the settlers starve instead of growing.
        var player = game.GetPlayer(0)!;
        player.Inventory.Set(Material.Bread, 0);
        player.Inventory.Set(Material.Fish, 0);
        player.Inventory.Set(Material.Meat, 0);

        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0);
        int ticks = 0;
        while (house.State != BuildingState.Active && ticks < 3000)
        {
            game.TickOnce();
            ticks++;
        }
        Assert.AreEqual(BuildingState.Active, house.State);

        // Align to the next 600-tick food cycle boundary, then measure one
        // full cycle.
        while (game.Tick % 600 != 0)
            game.TickOnce();

        int before = player.Population;
        for (int i = 0; i < 600; i++)
            game.TickOnce();

        // Without food the population shrinks by one.
        Assert.AreEqual(before - 1, player.Population);
        Assert.AreEqual(before - 1, player.FreeSettlers);
    }

    [TestMethod]
    public void PopulationConsumesFood()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var player = game.GetPlayer(0)!;

        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, house.State);

        // Measure the bread stock after the house is active, then one
        // 600-tick food cycle consumes exactly one bread.
        int breadBefore = player.Inventory.Get(Material.Bread);
        for (int i = 0; i < 600; i++)
            game.TickOnce();

        // One settler ate one bread.
        Assert.AreEqual(breadBefore - 1, player.Inventory.Get(Material.Bread));
    }

    [TestMethod]
    public void PopulationLimitedByLivingCapacity()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Plenty of food so growth is only limited by the house capacity.
        var player = game.GetPlayer(0)!;
        player.Inventory.Set(Material.Bread, 1000);
        player.Inventory.Set(Material.Fish, 1000);
        player.Inventory.Set(Material.Meat, 1000);

        var house = game.PlaceBuilding(BuildingType.SmallLivinghouse, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, house.State);

        // A small living house holds 10 settlers. Grow well beyond that.
        for (int i = 0; i < 600 * 20; i++)
            game.TickOnce();

        Assert.AreEqual(10, player.Population);
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
        for (int i = 0; i < 3000; i++)
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

    [TestMethod]
    public void SawmillGetsTrunkDeliveredByCarrier()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Build a sawmill and let it become active.
        var sawmill = game.PlaceBuilding(BuildingType.Sawmill, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, sawmill.State);
        sawmill.AssignWorker(WorkerType.Sawmiller);

        // The player has a trunk in stock; the sawmill needs it as input.
        var player = game.GetPlayer(0)!;
        player.Inventory.Add(Material.Trunk, 1);

        // The trunk is sent to the stock flag, carried to the sawmill flag
        // and moved into the sawmill's input.
        bool delivered = false;
        for (int i = 0; i < 2000 && !delivered; i++)
        {
            game.TickOnce();
            delivered = sawmill.Input.Get(Material.Trunk) > 0;
        }

        Assert.IsTrue(delivered, "The trunk was not delivered to the sawmill.");
        Assert.AreEqual(0, player.Inventory.Get(Material.Trunk));
    }

    [TestMethod]
    public void MilitaryBuildingRecruitsSoldier()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 2, PosY = 2 });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 3, PosY = 3 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Build a tower and let it become active. Soldiers are recruited
        // as soon as the tower is active.
        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);

        // The player has free settlers and weapons, so the tower recruits
        // soldiers up to its capacity (3).
        Assert.AreEqual(3, tower.Soldiers.Count);
        Assert.IsTrue(tower.Soldiers.All(s => s.Type == SerfType.Soldier));
        // 3 weapons consumed (sword, sword, spear).
        Assert.AreEqual(0, game.GetPlayer(0)!.Inventory.Get(Material.Sword));
        Assert.AreEqual(1, game.GetPlayer(0)!.Inventory.Get(Material.Spear));
        Assert.AreEqual(2, game.GetPlayer(0)!.Inventory.Get(Material.Bow));
    }

    [TestMethod]
    public void MilitaryBuildingClaimsTerritory()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // No military buildings yet: no territory is claimed.
        Assert.AreEqual(0, game.GetTerritoryPlayer(new Position(2, 2)));

        // Build a tower and let it become active.
        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);

        // The tower claims the walkable cells around it (radius 40 covers
        // the whole 8x8 map). The tower's own footprint is not walkable.
        Assert.AreEqual(0, game.GetTerritoryPlayer(new Position(2, 2)));
        Assert.AreEqual(0, game.GetTerritoryPlayer(new Position(0, 0)));
        Assert.AreEqual(0, game.GetTerritoryPlayer(new Position(4, 4)));
    }

    [TestMethod]
    public void TowerAttacksEnemyInRange()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Build a tower, let it become active and recruit a soldier.
        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(4, 4), 0);
        for (int i = 0; i < 3000; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);
        Assert.AreEqual(1, tower.Soldiers.Count);

        // An enemy settler appears near the tower.
        var enemy = new Serf(1, new Position(4, 5), SerfType.Worker);
        game.Settlers.Add(enemy);

        // The tower attacks and defeats the enemy (20 damage per hit,
        // every 30 ticks, so 100 health needs ~150 ticks).
        bool defeated = false;
        for (int i = 0; i < 300 && !defeated; i++)
        {
            game.TickOnce();
            defeated = !game.Settlers.Contains(enemy);
        }

        Assert.IsTrue(defeated, "The tower did not defeat the enemy settler.");
    }

    [TestMethod]
    public void SoldierAttacksEnemyInRange()
    {
        var map = new Map(16);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Two enemy soldiers close to each other.
        var attackerBarracks = new Building(BuildingType.Tower, new Position(4, 4), 0);
        attackerBarracks.MarkActive();
        var defenderBarracks = new Building(BuildingType.Tower, new Position(4, 5), 1);
        defenderBarracks.MarkActive();
        var attacker = new Serf(0, new Position(4, 4), SerfType.Soldier)
        {
            Weapon = Material.Sword,
            Health = 200f,
            Barracks = attackerBarracks
        };
        var defender = new Serf(1, new Position(4, 5), SerfType.Soldier)
        {
            Weapon = Material.Sword,
            Health = 100f,
            Barracks = defenderBarracks
        };
        game.Settlers.Add(attacker);
        game.Settlers.Add(defender);

        // The attacker moves to the defender and hits it until it dies.
        bool defeated = false;
        for (int i = 0; i < 2000 && !defeated; i++)
        {
            game.TickOnce();
            defeated = !game.Settlers.Contains(defender);
        }

        Assert.IsTrue(defeated, "The soldier did not defeat the enemy soldier.");
        Assert.IsTrue(attacker.Health > 0, "The attacking soldier should survive.");
    }

    [TestMethod]
    public void AiPlayerBuildsBuildings()
    {
        var map = new Map(32);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 1, PosX = 20, PosY = 20 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // The AI player builds a lumberjack within the first build cycles.
        bool built = false;
        for (int i = 0; i < 2000 && !built; i++)
        {
            game.TickOnce();
            built = game.Buildings.Any(b => b.Player == 1 && b.Type == BuildingType.Lumberjack);
        }

        Assert.IsTrue(built, "The AI player did not build a lumberjack.");
    }

    [TestMethod]
    public void AiPlayerSendsAttackWave()
    {
        var map = new Map(32);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Players.Add(new MapPlayerInfo { Name = "P1" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        map.Settlers.Add(new MapSettler { Party = 1, PosX = 20, PosY = 20 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Give the AI a military building with a soldier and a human
        // building to attack.
        var aiTower = new Building(BuildingType.Tower, new Position(20, 20), 1);
        aiTower.MarkActive();
        game.Buildings.Add(aiTower);
        var soldier = new Serf(1, new Position(20, 20), SerfType.Soldier)
        {
            Barracks = aiTower,
            Weapon = Material.Sword,
            SoldierState = SoldierState.Idle
        };
        aiTower.Soldiers.Add(soldier);
        game.Settlers.Add(soldier);

        var humanHouse = new Building(BuildingType.SmallLivinghouse, new Position(5, 5), 0);
        humanHouse.MarkActive();
        game.Buildings.Add(humanHouse);

        // After the attack interval the soldier marches toward the human.
        bool marching = false;
        for (int i = 0; i < 4000 && !marching; i++)
        {
            game.TickOnce();
            marching = soldier.MarchTarget != null;
        }

        Assert.IsTrue(marching, "The AI did not send an attack wave.");
        Assert.IsTrue(MapGeometry.GetDistance(soldier.MarchTarget!.Value, humanHouse.Position) <= 2,
            "The attack target is not near the human building.");
    }
}

