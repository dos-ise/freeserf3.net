/*
 * MapTests.cs - Tests for the game map and the map modifier
 */

using Freeserf3.Data;
using Freeserf3.Game;

namespace Freeserf3.Test;

[TestClass]
public class MapTests : DataTestBase
{
    [TestMethod]
    public void EmptyMapHasDefaultCells()
    {
        var map = new Map(16);

        Assert.AreEqual(16, map.Size);
        Assert.AreEqual(256, map.CellCount);

        var cell = map.GetCell(new Position(3, 4));
        Assert.AreEqual(0, cell.Height);
        Assert.AreEqual(RoadState.None, cell.Road);
        Assert.IsFalse(cell.HasFlag);
    }

    [TestMethod]
    public void MapRejectsOutOfBoundsAccess()
    {
        var map = new Map(8);

        Assert.IsFalse(map.IsInBounds(new Position(-1, 0)));
        Assert.IsFalse(map.IsInBounds(new Position(0, 8)));
        Assert.IsFalse(map.IsInBounds(new Position(8, 0)));
        Assert.IsTrue(map.IsInBounds(new Position(7, 7)));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.GetCell(new Position(8, 0)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.SetCell(new Position(0, -1), new MapCell()));
    }

    [TestMethod]
    public void SetAndGetCellRoundTrip()
    {
        var map = new Map(16);
        var pos = new Position(5, 9);

        var cell = new MapCell
        {
            Height = 12,
            Landscape = 16,
            MapObject = 3,
            Player = 1,
            Accessible = 1,
            ResourceType1 = 2,
            ResourceType2 = 5,
            Road = RoadState.Road,
            HasFlag = true
        };

        map.SetCell(pos, cell);
        var read = map.GetCell(pos);

        Assert.AreEqual(12, read.Height);
        Assert.AreEqual(16, read.Landscape);
        Assert.AreEqual(3, read.MapObject);
        Assert.AreEqual(1, read.Player);
        Assert.AreEqual(1, read.Accessible);
        Assert.AreEqual(2, read.ResourceType1);
        Assert.AreEqual(5, read.ResourceType2);
        Assert.AreEqual(RoadState.Road, read.Road);
        Assert.IsTrue(read.HasFlag);
    }

    [TestMethod]
    public void AllPositionsCoversEveryCell()
    {
        var map = new Map(4);
        var positions = map.AllPositions().ToList();

        Assert.AreEqual(16, positions.Count);
        Assert.IsTrue(positions.Contains(new Position(0, 0)));
        Assert.IsTrue(positions.Contains(new Position(3, 3)));
        Assert.AreEqual(16, positions.Distinct().Count());
    }

    [TestMethod]
    public void NeighborsAreInBounds()
    {
        var map = new Map(4);

        // On the hex grid a corner cell has 3 in-bounds neighbours,
        // an edge cell 4 and an inner cell 6.
        Assert.HasCount(3, map.Neighbors(new Position(0, 0)));
        Assert.HasCount(4, map.Neighbors(new Position(0, 2)));
        Assert.HasCount(6, map.Neighbors(new Position(2, 2)));
    }

    [TestMethod]
    public void ModifierChangesTerrain()
    {
        var map = new Map(16);
        var modifier = new MapModifier(map);
        var pos = new Position(2, 2);

        modifier.SetHeight(pos, 20);
        modifier.SetLandscape(pos, 16);
        modifier.SetObject(pos, 7);
        modifier.SetPlayer(pos, 2);
        modifier.SetAccessible(pos, 1);
        modifier.SetResources(pos, 1, 3);
        modifier.PlaceRoad(pos);
        modifier.SetFlag(pos, true);

        var cell = map.GetCell(pos);
        Assert.AreEqual(20, cell.Height);
        Assert.AreEqual(16, cell.Landscape);
        Assert.AreEqual(7, cell.MapObject);
        Assert.AreEqual(2, cell.Player);
        Assert.AreEqual(1, cell.Accessible);
        Assert.AreEqual(1, cell.ResourceType1);
        Assert.AreEqual(3, cell.ResourceType2);
        Assert.AreEqual(RoadState.Road, cell.Road);
        Assert.IsTrue(cell.HasFlag);

        modifier.RemoveRoad(pos);
        modifier.SetFlag(pos, false);
        cell = map.GetCell(pos);
        Assert.AreEqual(RoadState.None, cell.Road);
        Assert.IsFalse(cell.HasFlag);
    }

    [TestMethod]
    public void ModifierFlattenSetsHeightAndAccessible()
    {
        var map = new Map(16);
        var modifier = new MapModifier(map);
        var pos = new Position(1, 1);

        modifier.Flatten(pos, 8);

        var cell = map.GetCell(pos);
        Assert.AreEqual(8, cell.Height);
        Assert.AreEqual(1, cell.Accessible);
    }

    [TestMethod]
    public void LoadsMapFromReader()
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
        reader.ReadBuildings();
        reader.ReadSettlers();
        reader.ReadStacks();

        var map = Map.FromReader(reader);

        Assert.AreEqual(reader.WidthHeight, map.Size);
        Assert.AreEqual(reader.PlayerCount, map.PlayerCount);
        Assert.AreEqual(reader.Buildings.Count, map.Buildings.Count);
        Assert.AreEqual(reader.Settlers.Count, map.Settlers.Count);
        Assert.AreEqual(reader.Stacks.Count, map.Stacks.Count);

        // Spot-check a cell against the raw reader data.
        var pos = new Position(0, 0);
        var cell = map.GetCell(pos);
        var node = reader.Area![0];
        Assert.AreEqual(node.Height, cell.Height);
        Assert.AreEqual(node.Landscape, cell.Landscape);
        Assert.AreEqual(node.MapObject, cell.MapObject);
        Assert.AreEqual(node.Player, cell.Player);
    }
}
