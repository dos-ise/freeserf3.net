/*
 * WindowsSoundPlayer.cs - Sound playback for the Windows host (NAudio)
 *
 * Sound effects are played on a small pool of WaveOutEvent devices so
 * several sounds can overlap. Music is played on a dedicated WaveOutEvent
 * with an Mp3FileReader.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Renderer;
using NAudio.Wave;

namespace Freeserf3;

/// <summary>NAudio-based sound player for the Windows host.</summary>
public sealed class WindowsSoundPlayer : ISoundPlayer, IDisposable
{
    const int MaxConcurrentSounds = 8;

    readonly WaveOutEvent[] sfxPlayers = new WaveOutEvent[MaxConcurrentSounds];
    WaveOutEvent? musicPlayer;
    Mp3FileReader? musicReader;

    public WindowsSoundPlayer()
    {
        for (int i = 0; i < MaxConcurrentSounds; i++)
            sfxPlayers[i] = new WaveOutEvent();
    }

    public bool SoundEnabled { get; set; } = true;
    public bool MusicEnabled { get; set; } = true;
    public float Volume { get; set; } = 1f;

    public bool IsMusicPlaying => musicPlayer?.PlaybackState == PlaybackState.Playing;

    public void PlaySound(short[] samples, int sampleRate, float volume)
    {
        // Find a free player from the pool.
        for (int i = 0; i < MaxConcurrentSounds; i++)
        {
            if (sfxPlayers[i].PlaybackState == PlaybackState.Playing)
                continue;

            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            var provider = new RawSourceWaveStream(new MemoryStream(bytes), new WaveFormat(sampleRate, 16, 1));
            sfxPlayers[i].Init(provider);
            sfxPlayers[i].Volume = Math.Clamp(volume * Volume, 0f, 1f);
            sfxPlayers[i].Play();
            return;
        }
    }

    public void PlayMusic(string filePath)
    {
        StopMusic();
        if (!File.Exists(filePath))
            return;

        musicReader = new Mp3FileReader(filePath);
        musicPlayer = new WaveOutEvent();
        musicPlayer.Init(musicReader);
        musicPlayer.Volume = Math.Clamp(Volume, 0f, 1f);
        musicPlayer.Play();
    }

    public void StopMusic()
    {
        musicPlayer?.Stop();
        musicPlayer?.Dispose();
        musicPlayer = null;
        musicReader?.Dispose();
        musicReader = null;
    }

    public void Dispose()
    {
        StopMusic();
        foreach (var player in sfxPlayers)
            player.Dispose();
    }
}
