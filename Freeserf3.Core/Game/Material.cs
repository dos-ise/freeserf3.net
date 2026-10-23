/*
 * Material.cs - The goods of the Settlers 3 economy
 *
 * The material list follows settlers-remake (MIT) EMaterialType.java.
 * The enum order matches the default priority order of the original game.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Simulation;

/// <summary>The goods that can be produced, stored and transported.</summary>
public enum Material
{
    Plank = 0,
    Stone = 1,
    Trunk = 2,
    Iron = 3,
    IronOre = 4,
    Coal = 5,
    Bread = 6,
    Fish = 7,
    Meat = 8,
    Flour = 9,
    Crop = 10,
    Pig = 11,
    Water = 12,
    Sword = 13,
    Bow = 14,
    Spear = 15,
    Blade = 16,
    Hammer = 17,
    Axe = 18,
    Pick = 19,
    Saw = 20,
    FishingRod = 21,
    Scythe = 22,
    GoldOre = 23,
    Gold = 24,
    Wine = 25,
    Mead = 26,
    Honey = 27,
    Gems = 28,
    Sulfur = 29,
    Rice = 30,
    Keg = 31,
    Liquor = 32,
    GunPowder = 33
}

/// <summary>Helpers for the material enum.</summary>
public static class MaterialExtensions
{
    /// <summary>Number of material types.</summary>
    public const int Count = 34;

    /// <summary>All materials in enum order.</summary>
    public static readonly Material[] All = Enum.GetValues<Material>();

    /// <summary>The weapons (used by soldiers).</summary>
    public static readonly Material[] Weapons = { Material.Sword, Material.Spear, Material.Bow };

    /// <summary>The tools (used by workers).</summary>
    public static readonly Material[] Tools =
    {
        Material.Hammer, Material.Blade, Material.Pick, Material.Axe,
        Material.Saw, Material.Scythe, Material.FishingRod
    };

    /// <summary>Whether the material is a weapon.</summary>
    public static bool IsWeapon(this Material material)
    {
        return material == Material.Sword || material == Material.Spear || material == Material.Bow;
    }

    /// <summary>Whether the material is a tool.</summary>
    public static bool IsTool(this Material material)
    {
        return material is Material.Hammer or Material.Blade or Material.Pick
            or Material.Axe or Material.Saw or Material.Scythe or Material.FishingRod;
    }
}
