/*
 * SoundManager.cs - Plays the sound effects of the Settlers 3 sound file
 *
 * Ported from settlers-remake (MIT) SoundManager.java. The sound file
 * (Siedler3_00.dat) contains 118 sound sequences with several alternatives
 * each; a random alternative is played per call.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;

namespace Freeserf3.Renderer;

/// <summary>
/// Plays the sound effects of the game. Sound ids follow the original
/// game (see settlers-remake SoundManager.java): 0 lumberjack, 3
/// stonecutter, 36 falling tree, 51 building construction, 62 UI click,
/// 57-59 notifications, ...
/// </summary>
public sealed class SoundManager
{
    readonly SoundReader reader;
    readonly ISoundPlayer player;
    readonly Random random = new();

    public SoundManager(SoundReader reader, ISoundPlayer player)
    {
        this.reader = reader;
        this.player = player;
    }

    /// <summary>Plays a sound with a random alternative.</summary>
    public void PlaySound(int soundId, float volume = 1f)
    {
        if (!player.SoundEnabled)
            return;

        int alternatives = reader.GetAlternativeCount(soundId);
        if (alternatives <= 0)
            return;

        int alternative = random.Next(alternatives);
        var data = reader.GetSoundData(soundId, alternative);
        if (data.Samples.Length == 0)
            return;

        player.PlaySound(data.Samples, data.SampleRate, volume);
    }
}
