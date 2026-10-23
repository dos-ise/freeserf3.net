/*
 * GameData.cs - Access to the original Settlers 3 game data folder
 *
 * The original game data is copyrighted and never bundled with this project.
 * The user must own the game and provide the data folder (e.g. a Settlers 3
 * installation or the Ubisoft Connect download).
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Data;

/// <summary>
/// Provides access to the original Settlers 3 game data files (GFX, SND,
/// Map, Theme) from a local folder.
/// </summary>
public sealed class GameData
{
    /// <summary>Landscape images are stored in dat file 0.</summary>
    public const int LandscapeFile = 0;

    readonly string rootPath;

    public GameData(string rootPath)
    {
        this.rootPath = rootPath;
    }

    public string RootPath => rootPath;

    public string GfxPath => Path.Combine(rootPath, "GFX");
    public string SndPath => Path.Combine(rootPath, "SND");
    public string MapPath => Path.Combine(rootPath, "Map");
    public string ThemePath => Path.Combine(rootPath, "Theme");

    /// <summary>Checks whether the folder looks like a Settlers 3 installation.</summary>
    public bool IsValid()
    {
        return Directory.Exists(GfxPath) && Directory.Exists(SndPath) && Directory.Exists(MapPath);
    }

    /// <summary>Finds the dat file with the given number (case-insensitive).</summary>
    public string? FindGfxFile(int number)
    {
        if (!Directory.Exists(GfxPath))
            return null;

        foreach (var file in Directory.EnumerateFiles(GfxPath))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith($"Siedler3_{number:D2}.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith($"siedler3_{number:D2}.", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        return null;
    }

    /// <summary>Loads a dat file reader for the given file number.</summary>
    public DatFileReader? LoadGfxFile(int number)
    {
        string? path = FindGfxFile(number);
        if (path == null)
            return null;

        byte[] data = File.ReadAllBytes(path);
        return new DatFileReader(data, DatFileReader.GetTypeForPath(path), Path.GetFileName(path));
    }

    /// <summary>Loads the sound file reader.</summary>
    public SoundReader? LoadSound()
    {
        string path = Path.Combine(SndPath, SoundReader.SoundFileName);
        if (!File.Exists(path))
            return null;

        return new SoundReader(File.ReadAllBytes(path));
    }

    /// <summary>Lists all .map files in the Map folder (recursively).</summary>
    public IEnumerable<string> FindMapFiles()
    {
        if (!Directory.Exists(MapPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(MapPath, "*.map", SearchOption.AllDirectories))
            yield return file;
    }

    /// <summary>Loads a map file reader.</summary>
    public MapFileReader LoadMap(string path)
    {
        return new MapFileReader(File.ReadAllBytes(path));
    }

    /// <summary>Lists all music tracks in the Theme folder.</summary>
    public IEnumerable<string> FindMusicFiles()
    {
        if (!Directory.Exists(ThemePath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(ThemePath, "*.mp3", SearchOption.TopDirectoryOnly))
            yield return file;
    }
}
