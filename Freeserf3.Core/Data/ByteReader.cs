/*
 * ByteReader.cs - Little-endian binary reader over a byte array
 *
 * Ported from settlers-remake (MIT) ByteReader.java.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Data;

/// <summary>
/// A little-endian random access reader over a byte array.
/// </summary>
public sealed class ByteReader
{
    readonly byte[] data;
    long position;

    public ByteReader(byte[] data)
    {
        this.data = data;
    }

    public long Position => position;

    public int Read8()
    {
        return data[position++] & 0xFF;
    }

    public int Read16()
    {
        int b0 = data[position++] & 0xFF;
        int b1 = data[position++] & 0xFF;
        return b0 | (b1 << 8);
    }

    public int Read16Signed()
    {
        int read = Read16();
        return read < 0x8000 ? read : read - 0x10000;
    }

    public int Read32()
    {
        int b0 = data[position++] & 0xFF;
        int b1 = data[position++] & 0xFF;
        int b2 = data[position++] & 0xFF;
        int b3 = data[position++] & 0xFF;
        return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
    }

    public void AssumeToRead(byte[] expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            byte read = data[position++];
            if (read != expected[i])
            {
                throw new InvalidDataException(
                    $"Expected to read {expected[i]:X2} but got {read:X2} (i={i})");
            }
        }
    }

    public void SkipTo(long pos)
    {
        position = pos;
    }

    public void Skip(int count)
    {
        position += count;
    }
}
