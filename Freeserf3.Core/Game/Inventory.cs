/*
 * Inventory.cs - Goods stock of a player or building
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>
/// A stock of goods, indexed by <see cref="Material"/>. Used for the
/// player's central store and for building inventories.
/// </summary>
public sealed class Inventory
{
    readonly int[] counts = new int[MaterialExtensions.Count];

    /// <summary>Gets the amount of the given material.</summary>
    public int Get(Material material)
    {
        return counts[(int)material];
    }

    /// <summary>Sets the amount of the given material.</summary>
    public void Set(Material material, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must not be negative.");

        counts[(int)material] = count;
    }

    /// <summary>Adds goods to the stock.</summary>
    public void Add(Material material, int count = 1)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must not be negative.");

        counts[(int)material] += count;
    }

    /// <summary>
    /// Removes goods from the stock. Returns false (and changes nothing)
    /// if the stock does not hold enough of the material.
    /// </summary>
    public bool Remove(Material material, int count = 1)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must not be negative.");

        if (counts[(int)material] < count)
            return false;

        counts[(int)material] -= count;
        return true;
    }

    /// <summary>Total number of goods in the stock.</summary>
    public int TotalCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < counts.Length; i++)
                total += counts[i];
            return total;
        }
    }

    /// <summary>Whether the stock holds no goods at all.</summary>
    public bool IsEmpty => TotalCount == 0;

    /// <summary>Total number of weapons in the stock.</summary>
    public int WeaponCount
    {
        get
        {
            int total = 0;
            foreach (var weapon in MaterialExtensions.Weapons)
                total += Get(weapon);
            return total;
        }
    }

    /// <summary>Total number of tools in the stock.</summary>
    public int ToolCount
    {
        get
        {
            int total = 0;
            foreach (var tool in MaterialExtensions.Tools)
                total += Get(tool);
            return total;
        }
    }

    /// <summary>Enumerates all materials with a positive count.</summary>
    public IEnumerable<(Material Material, int Count)> NonEmpty()
    {
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
                yield return ((Material)i, counts[i]);
        }
    }
}
