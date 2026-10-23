/*
 * SoundReader.cs - Reader for the Settlers 3 sound file (Siedler3_00.dat)
 *
 * Ported from settlers-remake (MIT) SoundManager.java.
 *
 * The sound file contains 118 sound sequences, each with several
 * alternatives. Each sound is 16-bit signed PCM.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Data;

/// <summary>A decoded sound (16-bit signed PCM samples).</summary>
public sealed class SoundData
{
    public short[] Samples { get; }
    public int SampleRate { get; }

    public SoundData(short[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }
}

/// <summary>
/// Reads the Settlers 3 sound file (Siedler3_00.dat). Provides the start
/// offsets of all sound sequences and lazy decoding of individual sounds.
/// </summary>
public sealed class SoundReader
{
    public const string SoundFileName = "Siedler3_00.dat";

    const int SoundMetaLength = 16;
    const int SoundFileStart = 0x24;
    const int SequenceN = 118;

    static readonly byte[] SoundFileMagic =
    {
        0x44, 0x15, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x1c, 0x00, 0x00, 0x00
    };

    readonly byte[] data;
    int[][]? soundStarts;

    public SoundReader(byte[] data)
    {
        this.data = data;
    }

    /// <summary>Number of sound sequences (118).</summary>
    public int SequenceCount => SequenceN;

    /// <summary>Reads the start offsets of all sound sequences.</summary>
    public void Initialize()
    {
        if (soundStarts != null)
            return;

        var reader = new ByteReader(data);
        reader.AssumeToRead(SoundFileMagic);
        reader.SkipTo(SoundFileStart);

        var sequenceHeaderStarts = new int[SequenceN];
        for (int i = 0; i < SequenceN; i++)
            sequenceHeaderStarts[i] = reader.Read32();

        soundStarts = new int[SequenceN][];
        for (int i = 0; i < SequenceN; i++)
        {
            reader.SkipTo(sequenceHeaderStarts[i]);
            int alternativeCount = reader.Read32();
            var starts = new int[alternativeCount];
            for (int j = 0; j < alternativeCount; j++)
                starts[j] = reader.Read32();
            soundStarts[i] = starts;
        }
    }

    /// <summary>Gets the number of alternatives for a sound sequence.</summary>
    public int GetAlternativeCount(int soundId)
    {
        Initialize();
        if (soundId < 0 || soundId >= SequenceN || soundStarts![soundId] == null)
            return 0;
        return soundStarts[soundId]!.Length;
    }

    /// <summary>Decodes one sound alternative (16-bit signed PCM).</summary>
    public SoundData GetSoundData(int soundId, int alternative = 0)
    {
        Initialize();
        if (soundId < 0 || soundId >= SequenceN || soundStarts![soundId] == null)
            return new SoundData([], 22050);

        var alternatives = soundStarts[soundId]!;
        if (alternative < 0 || alternative >= alternatives.Length)
            return new SoundData([], 22050);

        return GetSoundDataAt(alternatives[alternative]);
    }

    SoundData GetSoundDataAt(int start)
    {
        var reader = new ByteReader(data);
        reader.SkipTo(start);

        int length = reader.Read32() / 2 - SoundMetaLength;
        reader.Read32();
        int sampleRate = reader.Read32(); // mostly 22050
        reader.Read32(); // mostly 44100
        reader.Read32();

        if (length < 0)
            return new SoundData([], sampleRate);

        var samples = new short[length];
        for (int i = 0; i < length; i++)
            samples[i] = (short)reader.Read16Signed();

        return new SoundData(samples, sampleRate);
    }
}
