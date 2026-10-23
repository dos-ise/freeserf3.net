/*
 * LandscapeMappingTests.cs - Tests for the landscape type mapping
 */

using Freeserf3.Data;

namespace Freeserf3.Test;

[TestClass]
public class LandscapeMappingTests : DataTestBase
{
    [TestMethod]
    public void MapsKnownLandscapeValues()
    {
        Assert.AreEqual(LandscapeType.Water1, LandscapeMapping.FromMapValue(0));
        Assert.AreEqual(LandscapeType.Water8, LandscapeMapping.FromMapValue(7));
        Assert.AreEqual(LandscapeType.Grass, LandscapeMapping.FromMapValue(16));
        Assert.AreEqual(LandscapeType.Mountain, LandscapeMapping.FromMapValue(32));
        Assert.AreEqual(LandscapeType.Sand, LandscapeMapping.FromMapValue(48));
        Assert.AreEqual(LandscapeType.Desert, LandscapeMapping.FromMapValue(64));
        Assert.AreEqual(LandscapeType.Moor, LandscapeMapping.FromMapValue(80));
        Assert.AreEqual(LandscapeType.River1, LandscapeMapping.FromMapValue(96));
        Assert.AreEqual(LandscapeType.Snow, LandscapeMapping.FromMapValue(128));
        Assert.AreEqual(LandscapeType.Mud, LandscapeMapping.FromMapValue(144));
        Assert.IsNull(LandscapeMapping.FromMapValue(255));
    }

    [TestMethod]
    public void GetsBaseImageNumbers()
    {
        Assert.AreEqual(0, LandscapeMapping.GetImageNumber(LandscapeType.Grass));
        Assert.AreEqual(17, LandscapeMapping.GetImageNumber(LandscapeType.Water1));
        Assert.AreEqual(10, LandscapeMapping.GetImageNumber(LandscapeType.Water8));
        Assert.AreEqual(21, LandscapeMapping.GetImageNumber(LandscapeType.Mountain));
        Assert.AreEqual(31, LandscapeMapping.GetImageNumber(LandscapeType.Sand));
        Assert.AreEqual(18, LandscapeMapping.GetImageNumber(LandscapeType.Desert));
    }

    [TestMethod]
    public void SameLandscapeUsesBaseImage()
    {
        Assert.AreEqual(0, LandscapeMapping.GetTextureIndex(LandscapeType.Grass, LandscapeType.Grass, LandscapeType.Grass, 0));
        Assert.AreEqual(17, LandscapeMapping.GetTextureIndex(LandscapeType.Water1, LandscapeType.Water1, LandscapeType.Water1, 0));
    }

    [TestMethod]
    public void BorderTransitionUsesBorderTexture()
    {
        // Grass -> Sand transition (base index 112).
        int index = LandscapeMapping.GetTextureIndex(LandscapeType.Grass, LandscapeType.Grass, LandscapeType.Sand, 0);
        Assert.IsTrue(index >= 112 && index <= 115, $"Unexpected border index {index}");
    }

    [TestMethod]
    public void MapLandscapeValuesAreValid()
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

        var unknown = new HashSet<byte>();
        foreach (var node in reader.Area!)
        {
            if (LandscapeMapping.FromMapValue(node.Landscape) == null)
                unknown.Add(node.Landscape);
        }

        Console.WriteLine($"Unknown landscape values: {string.Join(", ", unknown.Select(v => v.ToString()))}");
        Assert.IsTrue(unknown.Count <= 2, $"Too many unknown landscape values: {string.Join(", ", unknown)}");
    }
}
