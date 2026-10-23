/*
 * Road.cs - A road connecting two flags
 *
 * Roads are the edges of the transport network. Goods sent along a road
 * move step by step from the start flag to the end flag.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// A road between two flags. Goods sent along the road move one step per
/// tick and arrive at the end flag.
/// </summary>
public sealed class Road
{
    readonly List<(Material Material, int Progress)> transit = new();

    public Road(Flag start, Flag end, List<Position> path)
    {
        Start = start;
        End = end;
        Path = path;
    }

    /// <summary>The flag the road starts at.</summary>
    public Flag Start { get; }

    /// <summary>The flag the road ends at.</summary>
    public Flag End { get; }

    /// <summary>The cells of the road (start flag position exclusive, end flag position inclusive).</summary>
    public List<Position> Path { get; }

    /// <summary>Number of cells of the road.</summary>
    public int Length => Path.Count;

    /// <summary>Number of goods currently in transit.</summary>
    public int TransitCount => transit.Count;

    /// <summary>Sends a good from the start flag to the end flag.</summary>
    public void SendGood(Material material)
    {
        transit.Add((material, 0));
    }

    /// <summary>Advances all goods in transit by one step.</summary>
    public void Tick()
    {
        for (int i = transit.Count - 1; i >= 0; i--)
        {
            var (material, progress) = transit[i];
            progress++;

            if (progress >= Path.Count)
            {
                End.AddGood(material);
                transit.RemoveAt(i);
            }
            else
            {
                transit[i] = (material, progress);
            }
        }
    }
}
