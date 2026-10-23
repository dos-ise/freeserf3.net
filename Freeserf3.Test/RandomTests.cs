/*
 * RandomTests.cs - Tests for the deterministic random generator
 */

using Freeserf3.Simulation;
using Random = Freeserf3.Simulation.Random;

namespace Freeserf3.Test;

[TestClass]
public class RandomTests
{
    [TestMethod]
    public void SameSeedProducesSameSequence()
    {
        var a = new Random(1234);
        var b = new Random(1234);

        for (int i = 0; i < 100; i++)
        {
            Assert.AreEqual(a.Next(), b.Next());
        }
    }

    [TestMethod]
    public void DifferentSeedProducesDifferentSequence()
    {
        var a = new Random(1234);
        var b = new Random(5678);

        bool anyDifferent = false;
        for (int i = 0; i < 100; i++)
        {
            if (a.Next() != b.Next())
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.IsTrue(anyDifferent);
    }

    [TestMethod]
    public void NextMaxStaysInRange()
    {
        var random = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            int value = random.Next(10);
            Assert.IsTrue(value >= 0 && value < 10);
        }
    }

    [TestMethod]
    public void NextMinMaxStaysInRange()
    {
        var random = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            int value = random.Next(5, 15);
            Assert.IsTrue(value >= 5 && value < 15);
        }
    }

    [TestMethod]
    public void NextDoubleStaysInUnitInterval()
    {
        var random = new Random(7);

        for (int i = 0; i < 1000; i++)
        {
            double value = random.NextDouble();
            Assert.IsTrue(value >= 0.0 && value < 1.0);
        }
    }

    [TestMethod]
    public void ChanceRespectsProbability()
    {
        var random = new Random(99);

        int hits = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (random.Chance(0.5))
                hits++;
        }

        // With 1000 samples the hit count must be far from the extremes.
        Assert.IsTrue(hits > 300 && hits < 700, $"Unexpected hit count: {hits}");
    }

    [TestMethod]
    public void NextMaxRejectsNonPositive()
    {
        var random = new Random(1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.Next(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.Next(-5));
    }
}
