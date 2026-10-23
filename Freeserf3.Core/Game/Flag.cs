/*
 * Flag.cs - A flag on the road network
 *
 * Flags are the nodes of the transport network. Goods are queued at flags
 * and carried along roads to the next flag.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// A flag on the map: a node of the transport network with a queue of
/// goods waiting to be transported.
/// </summary>
public sealed class Flag
{
    public Flag(Position position, int player)
    {
        Position = position;
        Player = player;
    }

    /// <summary>Position of the flag on the map.</summary>
    public Position Position { get; }

    /// <summary>Owning player.</summary>
    public int Player { get; }

    /// <summary>Goods queued at this flag (FIFO).</summary>
    public Queue<Material> Queue { get; } = new();

    /// <summary>Number of goods queued.</summary>
    public int Count => Queue.Count;

    /// <summary>Adds a good to the queue.</summary>
    public void AddGood(Material material)
    {
        Queue.Enqueue(material);
    }

    /// <summary>Takes the next good from the queue, or null if empty.</summary>
    public Material? TakeGood()
    {
        return Queue.Count > 0 ? Queue.Dequeue() : null;
    }

    /// <summary>Whether the queue holds the given material.</summary>
    public bool Contains(Material material)
    {
        return Queue.Contains(material);
    }

    /// <summary>
    /// Takes the first occurrence of the given material from the queue, or
    /// null if it is not present.
    /// </summary>
    public Material? TakeGood(Material material)
    {
        if (!Queue.Contains(material))
            return null;

        var list = Queue.ToList();
        list.Remove(material);
        Queue.Clear();
        foreach (var m in list)
            Queue.Enqueue(m);
        return material;
    }
}
