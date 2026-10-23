/*
 * DatFileReaderTests.cs - Tests for the GFX .dat reader against real data
 */

using Freeserf3.Data;

namespace Freeserf3.Test;

[TestClass]
public class DatFileReaderTests : DataTestBase
{
    [TestMethod]
    public void LoadsLandscapeImagesFromFile0()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var reader = gameData.LoadGfxFile(0);

        Assert.IsNotNull(reader, "Siedler3_00.dat not found");
        Assert.IsTrue(reader!.LandscapeCount > 0, "No landscape images found");
        Console.WriteLine($"Landscape count: {reader.LandscapeCount}");
        Console.WriteLine($"Settler count: {reader.SettlerCount}");
        Console.WriteLine($"GUI count: {reader.GuiCount}");
    }

    [TestMethod]
    public void DecodesLandscapeImage()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var reader = gameData.LoadGfxFile(0)!;

        // Decode the first few landscape images and verify dimensions.
        for (int i = 0; i < Math.Min(5, reader.LandscapeCount); i++)
        {
            var image = reader.GetLandscape(i);
            Assert.IsNotNull(image, $"Landscape image {i} is null");
            Assert.IsTrue(image!.Width > 0 && image.Height > 0, $"Landscape {i} has invalid size {image.Width}x{image.Height}");
            Assert.AreEqual(image.Width * image.Height * 4, image.Rgba.Length);
            Console.WriteLine($"Landscape {i}: {image.Width}x{image.Height} offset=({image.OffsetX},{image.OffsetY})");
        }
    }

    [TestMethod]
    public void DecodesSettlerSequence()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        // Settler sequences are in file 1 (140 sequences).
        var reader = gameData.LoadGfxFile(1)!;

        if (reader.SettlerCount == 0)
        {
            Assert.Inconclusive("No settler sequences in file 1.");
            return;
        }

        var sequence = reader.GetSettlerSequence(0);
        Assert.IsTrue(sequence.Length > 0, "Settler sequence 0 is empty");
        foreach (var image in sequence)
        {
            Assert.IsTrue(image.Width > 0 && image.Height > 0);
        }
        Console.WriteLine($"Settler sequence 0: {sequence.Length} frames, first {sequence[0].Width}x{sequence[0].Height}");
    }

    [TestMethod]
    public void DetectsFileType()
    {
        Assert.AreEqual(DatFileType.Rgb565, DatFileReader.GetTypeForPath("Siedler3_00.f8007e01f.dat"));
        Assert.AreEqual(DatFileType.Rgb555, DatFileReader.GetTypeForPath("Siedler3_00.7c003e01f.dat"));
    }
}
