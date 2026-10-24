/*
 * FogOfWarTests.cs - Tests for the fog of war visibility grid
 */

using Freeserf3.Data;
using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class FogOfWarTests
{
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
    public void MapStartsUnexplored()
    {
        var map = new Map(8);
        var fog = new FogOfWar(map);

        Assert.AreEqual(FogState.Unexplored, fog.GetSight(new Position(3, 3)));
        Assert.AreEqual(FogState.Unexplored, fog.GetSight(0, 0));
    }

    [TestMethod]
    public void SettlerRevealsCircle()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var fog = new FogOfWar(map);
        fog.Update(game);

        // The settler at (1,1) reveals a circle of radius 8 (covers the
        // whole 8x8 map).
        Assert.AreEqual(FogState.Visible, fog.GetSight(new Position(0, 0)));
        Assert.AreEqual(FogState.Visible, fog.GetSight(new Position(7, 7)));
    }

    [TestMethod]
    public void VisibleBecomesExplored()
    {
        var map = new Map(8);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 1, PosY = 1 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        var fog = new FogOfWar(map);
        fog.Update(game);
        Assert.AreEqual(FogState.Visible, fog.GetSight(new Position(0, 0)));

        // Remove the settler: the visible cells become explored.
        game.Settlers.Clear();
        fog.Update(game);
        Assert.AreEqual(FogState.Explored, fog.GetSight(new Position(0, 0)));
    }

    [TestMethod]
    public void MilitaryBuildingRevealsViewDistance()
    {
        var map = new Map(64);
        map.Players.Add(new MapPlayerInfo { Name = "P0" });
        map.Settlers.Add(new MapSettler { Party = 0, PosX = 5, PosY = 5 });
        SetGrass(map);
        var game = new Game(map, 42);
        game.StartGame();

        // Build a tower at (32,32) and let it become active.
        var tower = game.PlaceBuilding(BuildingType.Tower, new Position(32, 32), 0);
        for (int i = 0; i < 500; i++)
            game.TickOnce();
        Assert.AreEqual(BuildingState.Active, tower.State);

        var fog = new FogOfWar(map);
        fog.Update(game);

        // The tower (view distance 38) reveals a cell 20 away.
        Assert.AreEqual(FogState.Visible, fog.GetSight(new Position(32, 52)));
        // A cell far outside the tower's radius and the settler's radius
        // stays unexplored.
        Assert.AreEqual(FogState.Unexplored, fog.GetSight(new Position(0, 63)));
    }
}
