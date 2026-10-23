/*
 * DataTestBase.cs - Base class for tests against the real Settlers 3 data
 *
 * The original game data is copyrighted and not part of this repository.
 * Tests are skipped when the data folder is not available.
 */

using Freeserf3.Data;

namespace Freeserf3.Test;

public abstract class DataTestBase
{
    // Path to the Settlers 3 installation. Override via environment variable
    // FREESERF3_DATA or the default local path.
    protected static readonly string? DataPath = FindDataPath();

    protected static bool HasData => DataPath != null && Directory.Exists(DataPath);

    static string? FindDataPath()
    {
        string? env = Environment.GetEnvironmentVariable("FREESERF3_DATA");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        string local = @"D:\thesettlers3";
        if (Directory.Exists(local))
            return local;

        return null;
    }

    protected static GameData CreateGameData()
    {
        Assert.IsNotNull(DataPath, "Settlers 3 data not found. Set FREESERF3_DATA.");
        return new GameData(DataPath!);
    }
}
