/*
 * MapFileReaderTests.cs - Tests for the .map reader against real data
 */

using Freeserf3.Data;

namespace Freeserf3.Test;

[TestClass]
public class MapFileReaderTests : DataTestBase
{
    [TestMethod]
    public void LoadsMapWithValidChecksum()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var mapFiles = gameData.FindMapFiles().ToList();

        Assert.IsTrue(mapFiles.Count > 0, "No map files found");
        Console.WriteLine($"Found {mapFiles.Count} map files");

        int checkedMaps = 0;
        foreach (var mapFile in mapFiles.Take(5))
        {
            var reader = gameData.LoadMap(mapFile);
            Assert.IsTrue(reader.IsChecksumValid(), $"Checksum invalid for {mapFile}");
            reader.LoadMapResources();
            reader.ReadBasicMapInformation();
            Assert.IsTrue(reader.WidthHeight > 0, $"Invalid map size for {mapFile}");
            Console.WriteLine($"{Path.GetFileName(mapFile)}: {reader.WidthHeight}x{reader.WidthHeight}, players={reader.PlayerCount}, single={reader.SinglePlayerMap}");
            checkedMaps++;
        }

        Assert.IsTrue(checkedMaps > 0);
    }

    [TestMethod]
    public void ReadsAreaGrid()
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

        Assert.IsNotNull(reader.Area);
        Assert.AreEqual(reader.WidthHeight * reader.WidthHeight, reader.Area!.Length);

        // Verify some terrain data is present.
        int nonZero = reader.Area.Count(n => n.Landscape != 0 || n.Height != 0);
        Assert.IsTrue(nonZero > 0, "Area grid is all zero");
        Console.WriteLine($"Area: {reader.WidthHeight}x{reader.WidthHeight}, non-zero cells: {nonZero}");
    }

    [TestMethod]
    public void ReadsBuildingsSettlersAndStacks()
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
        reader.ReadBuildings();
        reader.ReadSettlers();
        reader.ReadStacks();

        Console.WriteLine($"Buildings: {reader.Buildings.Count}, Settlers: {reader.Settlers.Count}, Stacks: {reader.Stacks.Count}");
        Console.WriteLine($"Players: {reader.Players.Count}");
        foreach (var p in reader.Players)
        {
            Console.WriteLine($"  Player: nation={p.Nation} start=({p.StartX},{p.StartY}) name='{p.Name}'");
        }
    }
}
