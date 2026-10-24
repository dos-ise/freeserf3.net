/*
 * PerformanceTests.cs - Simulation performance regression tests
 *
 * These tests run the real simulation for many ticks and measure the
 * average tick time. They guard against regressions like stuck settlers
 * running the pathfinder every tick, which made the tick time grow
 * without bound (up to 22 ms/tick).
 *
 * The thresholds are generous so the tests do not fail on slow machines,
 * but they still catch the unbounded-growth regression.
 */

using System.Diagnostics;
using Freeserf3.Data;
using Freeserf3.Simulation;

namespace Freeserf3.Test;

[TestClass]
public class PerformanceTests : DataTestBase
{
    /// <summary>
    /// Loads the first single-player map and starts a game on it, or
    /// returns null when the game data is not available.
    /// </summary>
    static Game? CreateGame()
    {
        if (!HasData)
            return null;

        var gameData = CreateGameData();
        var mapFile = gameData.FindSinglePlayerMapFiles().FirstOrDefault();
        if (mapFile == null)
            return null;

        var reader = gameData.LoadMap(mapFile);
        reader.LoadMapResources();
        reader.ReadBasicMapInformation();
        reader.ReadMapData();
        reader.ReadBuildings();
        reader.ReadSettlers();
        reader.ReadStacks();

        var game = new Game(Map.FromReader(reader), 42);
        game.StartGame();
        return game;
    }

    /// <summary>Runs the given number of ticks and returns the average tick time in microseconds.</summary>
    static double MeasureTicks(Game game, int ticks)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ticks; i++)
            game.TickOnce();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds * 1000.0 / ticks;
    }

    [TestMethod]
    public void TickTimeStaysBelowThreshold()
    {
        var game = CreateGame();
        if (game == null)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        // Let the game grow (AI builds, soldiers accumulate) so the
        // worst-case path is exercised.
        for (int i = 0; i < 20000; i++)
            game.TickOnce();

        double avgUs = MeasureTicks(game, 5000);
        Console.WriteLine($"Average tick time after growth: {avgUs:F1} us");

        // Generous bound: the regression reached 22 ms/tick; a healthy
        // simulation stays well below 5 ms even on slow machines.
        Assert.IsTrue(avgUs < 5000, $"Average tick time {avgUs:F1} us exceeds 5000 us.");
    }

    [TestMethod]
    public void TickTimeDoesNotGrowUnboundedly()
    {
        var game = CreateGame();
        if (game == null)
        {
            Assert.Inconclusive("Settlers 3 data not available.");
            return;
        }

        // Measure early and late tick times. The regression made the tick
        // time grow from ~0.8 ms to ~22 ms; a healthy simulation stays
        // roughly flat.
        double earlyUs = MeasureTicks(game, 2000);
        for (int i = 0; i < 18000; i++)
            game.TickOnce();
        double lateUs = MeasureTicks(game, 2000);

        Console.WriteLine($"Early tick time: {earlyUs:F1} us, late tick time: {lateUs:F1} us");

        // The late tick time must not be more than 10x the early one.
        Assert.IsTrue(lateUs < earlyUs * 10 + 1000,
            $"Tick time grew from {earlyUs:F1} us to {lateUs:F1} us.");
    }
}
