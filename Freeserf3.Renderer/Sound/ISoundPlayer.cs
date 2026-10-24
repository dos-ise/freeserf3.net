/*
 * ISoundPlayer.cs - Platform-agnostic sound playback
 *
 * The Windows and Android hosts provide their own implementation (NAudio
 * on Windows, AudioTrack/MediaPlayer on Android), like settlers-remake
 * where the desktop uses OpenAL and Android uses AudioTrack.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Renderer;

/// <summary>
/// Plays sound effects and music. Implemented per platform: the Windows
/// host uses NAudio, the Android host uses AudioTrack and MediaPlayer.
/// </summary>
public interface ISoundPlayer
{
    /// <summary>Plays a sound effect (16-bit signed PCM mono).</summary>
    void PlaySound(short[] samples, int sampleRate, float volume);

    /// <summary>Plays a music track from the given file.</summary>
    void PlayMusic(string filePath);

    /// <summary>Stops the currently playing music.</summary>
    void StopMusic();

    /// <summary>Whether music is currently playing.</summary>
    bool IsMusicPlaying { get; }

    /// <summary>Whether sound effects are enabled.</summary>
    bool SoundEnabled { get; set; }

    /// <summary>Whether music is enabled.</summary>
    bool MusicEnabled { get; set; }

    /// <summary>Master volume (0..1).</summary>
    float Volume { get; set; }
}
