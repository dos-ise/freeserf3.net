/*
 * Program.cs - freeserf3.net desktop entry point
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;

namespace Freeserf3;

public static class Program
{
    public static string? DataPath { get; private set; }
    public static string? MapFile { get; private set; }

    public static int Main(string[] args)
    {
        try
        {
            ParseArgs(args);

            if (DataPath == null)
            {
                DataPath = FindDefaultDataPath();
            }

            if (DataPath == null || !Directory.Exists(DataPath))
            {
                Console.Error.WriteLine("Settlers 3 game data not found.");
                Console.Error.WriteLine("Usage: Freeserf3 -d <data-folder> [-m <map-file>]");
                Console.Error.WriteLine("The data folder must contain GFX, SND and Map subfolders.");
                return 1;
            }

            var gameData = new GameData(DataPath);
            if (!gameData.IsValid())
            {
                Console.Error.WriteLine($"'{DataPath}' does not look like a Settlers 3 installation (missing GFX/SND/Map).");
                return 1;
            }

            using var window = new MainWindow(gameData);
            window.Run();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Fatal error: {e}");
            return 1;
        }
    }

    static void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-d":
                case "--data":
                    if (i + 1 < args.Length)
                        DataPath = args[++i];
                    break;
                case "-m":
                case "--map":
                    if (i + 1 < args.Length)
                        MapFile = args[++i];
                    break;
                case "-h":
                case "--help":
                    Console.WriteLine("Usage: Freeserf3 -d <data-folder> [-m <map-file>]");
                    Environment.Exit(0);
                    break;
            }
        }
    }

    static string? FindDefaultDataPath()
    {
        string? env = Environment.GetEnvironmentVariable("FREESERF3_DATA");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        string local = @"D:\thesettlers3";
        if (Directory.Exists(local))
            return local;

        return null;
    }
}
