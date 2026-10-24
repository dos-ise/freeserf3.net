/*
 * AndroidSoundPlayer.cs - Sound playback for the Android host
 *
 * Sound effects are played on a pool of AudioTrack devices (like
 * settlers-remake AndroidSoundPlayer). Music is played with a MediaPlayer.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Android.Media;
using Freeserf3.Renderer;

namespace Freeserf3.Android;

/// <summary>AudioTrack/MediaPlayer based sound player for the Android host.</summary>
public sealed class AndroidSoundPlayer : ISoundPlayer, IDisposable
{
    const int MaxConcurrentSounds = 8;

    readonly AudioTrack?[] sfxTracks = new AudioTrack?[MaxConcurrentSounds];
    readonly object sync = new();
    MediaPlayer? musicPlayer;
    bool musicPreparing;

    public bool SoundEnabled { get; set; } = true;
    public bool MusicEnabled { get; set; } = true;
    public float Volume { get; set; } = 1f;

    public bool IsMusicPlaying => musicPlayer?.IsPlaying == true;

    public void PlaySound(short[] samples, int sampleRate, float volume)
    {
        lock (sync)
        {
            for (int i = 0; i < MaxConcurrentSounds; i++)
            {
                var track = sfxTracks[i];
                if (track != null && track.PlayState == PlayState.Playing)
                    continue;

                if (track != null)
                {
                    track.Stop();
                    track.Release();
                }

                int minBuffer = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
                int bufferSize = Math.Max(samples.Length * 2, minBuffer);
                track = new AudioTrack(global::Android.Media.Stream.Music, sampleRate, ChannelConfiguration.Mono, Encoding.Pcm16bit, bufferSize, AudioTrackMode.Static);
                track.SetStereoVolume(Math.Clamp(volume * Volume, 0f, 1f), Math.Clamp(volume * Volume, 0f, 1f));
                track.Write(samples, 0, samples.Length);
                track.Play();
                sfxTracks[i] = track;
                return;
            }
        }
    }

    public void PlayMusic(string filePath)
    {
        StopMusic();
        if (!System.IO.File.Exists(filePath))
            return;

        var player = new MediaPlayer();
        player.SetDataSource(filePath);
        musicPreparing = true;
        player.Prepared += (s, e) =>
        {
            musicPreparing = false;
            player.SetVolume(Volume, Volume);
            player.Start();
        };
        player.Error += (s, e) => musicPreparing = false;
        player.PrepareAsync();
        musicPlayer = player;
    }

    public void StopMusic()
    {
        musicPreparing = false;
        musicPlayer?.Stop();
        musicPlayer?.Release();
        musicPlayer = null;
    }

    public void Dispose()
    {
        StopMusic();
        lock (sync)
        {
            for (int i = 0; i < MaxConcurrentSounds; i++)
            {
                sfxTracks[i]?.Stop();
                sfxTracks[i]?.Release();
                sfxTracks[i] = null;
            }
        }
    }
}
