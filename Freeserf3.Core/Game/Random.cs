/*
 * Random.cs - Deterministic random number generator
 *
 * Ported from freeserf.net (GPL v3) Random.cs. A small 3-word generator
 * that produces reproducible sequences for a given seed, so that games
 * can be replayed deterministically.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// Deterministic pseudo random number generator. Two instances with the
/// same seed produce the same sequence of values.
/// </summary>
public sealed class Random
{
    readonly ushort[] state = new ushort[3];

    public Random(ushort seed)
    {
        state[0] = seed;
        state[1] = seed;
        state[2] = seed;
    }

    public Random(ushort base0, ushort base1, ushort base2)
    {
        state[0] = base0;
        state[1] = base1;
        state[2] = base2;
    }

    /// <summary>Returns the next 16-bit value of the sequence.</summary>
    public ushort Next()
    {
        ushort result = (ushort)((state[0] + state[1]) ^ state[2]);
        state[2] += state[1];
        state[1] ^= state[2];
        state[1] = (ushort)((state[1] >> 1) | (state[1] << 15));
        state[2] = (ushort)((state[2] >> 1) | (state[2] << 15));
        state[0] = result;
        return result;
    }

    /// <summary>Returns a value in [0, max).</summary>
    public int Next(int max)
    {
        if (max <= 0)
            throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");

        return Next() % max;
    }

    /// <summary>Returns a value in [min, max).</summary>
    public int Next(int min, int max)
    {
        if (max <= min)
            throw new ArgumentOutOfRangeException(nameof(max), "max must be greater than min.");

        return min + Next(max - min);
    }

    /// <summary>Returns a value in [0, 1).</summary>
    public double NextDouble()
    {
        return Next() / 65536.0;
    }

    /// <summary>Returns true with the given probability (0..1).</summary>
    public bool Chance(double probability)
    {
        return NextDouble() < probability;
    }
}
