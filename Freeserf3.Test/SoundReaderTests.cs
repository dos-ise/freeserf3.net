/*
 * SoundReaderTests.cs - Tests for the sound reader against real data
 */

using Freeserf3.Data;

namespace Freeserf3.Test;

[TestClass]
public class SoundReaderTests : DataTestBase
{
    [TestMethod]
    public void LoadsSoundSequences()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var reader = gameData.LoadSound();

        Assert.IsNotNull(reader, "Siedler3_00.dat not found");
        Assert.AreEqual(118, reader!.SequenceCount);
    }

    [TestMethod]
    public void DecodesSound()
    {
        if (!HasData)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        var gameData = CreateGameData();
        var reader = gameData.LoadSound()!;

        // Sound 0 = lumberjack, 42 = mill, 62 = UI click, 68 = sea.
        foreach (int soundId in new[] { 0, 42, 62, 68 })
        {
            int alternatives = reader.GetAlternativeCount(soundId);
            Assert.IsTrue(alternatives > 0, $"Sound {soundId} has no alternatives");
            var sound = reader.GetSoundData(soundId, 0);
            Assert.IsTrue(sound.Samples.Length > 0, $"Sound {soundId} has no samples");
            Console.WriteLine($"Sound {soundId}: {alternatives} alternatives, {sound.Samples.Length} samples, rate={sound.SampleRate}");
        }
    }
}
