/*
 * ResourceStack.cs - A pile of resources on the map
 *
 * Resource stacks are placed on the map (e.g. starting resources). Settlers
 * can pick up goods from them and carry them to the player's stock.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>A pile of one material on the map.</summary>
public sealed class ResourceStack
{
    public ResourceStack(Position position, Material material, int count)
    {
        Position = position;
        Material = material;
        Count = count;
    }

    /// <summary>Position of the stack on the map.</summary>
    public Position Position { get; }

    /// <summary>The material of the stack.</summary>
    public Material Material { get; }

    /// <summary>Number of goods on the stack.</summary>
    public int Count { get; set; }

    /// <summary>Whether the stack is empty.</summary>
    public bool IsEmpty => Count <= 0;

    /// <summary>Takes one good from the stack, or null if empty.</summary>
    public Material? TakeOne()
    {
        if (Count <= 0)
            return null;
        Count--;
        return Material;
    }

    /// <summary>
    /// Maps a map-file stack type byte to a material (see the original map
    /// stack types in settlers-remake EOriginalMapStackType).
    /// </summary>
    public static Material? FromMapStackType(byte stackType)
    {
        return stackType switch
        {
            1 => Material.Plank,
            2 => Material.Stone,
            3 => Material.Trunk,
            4 => Material.Bread,
            5 => Material.Coal,
            6 => Material.GoldOre,
            7 => Material.IronOre,
            8 => Material.Fish,
            9 => Material.Crop,
            10 => Material.Gold,
            11 => Material.Iron,
            12 => Material.Blade,
            13 => Material.Hammer,
            14 => Material.Axe,
            15 => Material.Pick,
            16 => Material.Saw,
            17 => Material.FishingRod,
            18 => Material.Sword,
            19 => Material.Bow,
            20 => Material.Spear,
            21 => Material.Wine,
            22 => Material.Flour,
            23 => Material.Pig,
            24 => Material.Meat,
            25 => Material.Sulfur,
            26 => Material.Water,
            27 => Material.Rice,
            28 => Material.Gems,
            29 => Material.Keg,
            32 => Material.Scythe,
            35 => Material.Honey,
            _ => null
        };
    }
}
