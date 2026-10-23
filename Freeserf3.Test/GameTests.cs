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
}
