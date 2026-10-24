/*
 * MusicManager.cs - Plays the music tracks of the game
 *
 * Ported from settlers-remake (MIT) MusicManager.java. The music tracks
 * live in the Theme folder (Track02.mp3 - Track15.mp3 for the history
 * edition). The tracks are shuffled and played one after another.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Renderer;

/// <summary>
/// Plays the music tracks of the game in a shuffled loop. The tracks are
/// loaded from the Theme folder of the game data.
/// </summary>
public sealed class MusicManager
{
    readonly ISoundPlayer player;
    readonly string themePath;
    readonly Random random = new();
    List<string> tracks = new();
    int nextTrack;

    public MusicManager(ISoundPlayer player, string themePath)
    {
        this.player = player;
        this.themePath = themePath;
    }

    /// <summary>Number of music tracks found in the Theme folder.</summary>
    public int TrackCount => tracks.Count;

    /// <summary>Scans the Theme folder and shuffles the track list.</summary>
    public void Load()
    {
        tracks = new List<string>();
        if (string.IsNullOrEmpty(themePath) || !Directory.Exists(themePath))
            return;

        tracks = Directory.GetFiles(themePath, "*.mp3")
            .Concat(Directory.GetFiles(themePath, "*.ogg"))
            .Concat(Directory.GetFiles(themePath, "*.wav"))
            .OrderBy(f => f)
            .ToList();

        // Shuffle so the order is not always the same.
        for (int i = tracks.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (tracks[i], tracks[j]) = (tracks[j], tracks[i]);
        }
        nextTrack = 0;
    }

    /// <summary>Starts the music (plays the first track).</summary>
    public void Start()
    {
        if (!player.MusicEnabled || tracks.Count == 0)
            return;
        PlayNext();
    }

    /// <summary>Stops the music.</summary>
    public void Stop()
    {
        player.StopMusic();
    }

    /// <summary>
    /// Advances to the next track when the current one has finished. Called
    /// once per frame by the game view.
    /// </summary>
    public void Update()
    {
        if (!player.MusicEnabled || tracks.Count == 0)
            return;
        if (!player.IsMusicPlaying)
            PlayNext();
    }

    void PlayNext()
    {
        if (tracks.Count == 0)
            return;

        string track = tracks[nextTrack % tracks.Count];
        nextTrack++;
        player.PlayMusic(track);
    }
}
