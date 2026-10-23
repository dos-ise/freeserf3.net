/*
 * LandscapeMapping.cs - Mapping of map landscape values to terrain images
 *
 * Ported from settlers-remake (MIT): OriginalLandscape.java,
 * ELandscapeType.java and the border texture table of Background.java.
 *
 * The map stores a landscape byte per cell. It maps to a landscape type,
 * which has a base image number in the GFX dat file (file 0). Transitions
 * between two landscape types use dedicated border textures.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Data;

/// <summary>Landscape types of the Settlers 3 terrain.</summary>
public enum LandscapeType
{
    Grass,
    DryGrass,
    Desert,
    Earth,
    Mountain,
    Snow,
    Sand,
    Flattened,
    MountainBorder,
    MountainBorderOuter,
    Moor,
    MoorBorder,
    MoorBorderOuter,
    FlattenedDesert,
    SharpFlattenedDesert,
    Gravel,
    DesertBorder,
    DesertBorderOuter,
    SnowBorder,
    SnowBorderOuter,
    Mud,
    MudBorder,
    MudBorderOuter,
    Road,
    DryEarth,
    Water1, Water2, Water3, Water4, Water5, Water6, Water7, Water8,
    River1, River2, River3, River4
}

/// <summary>
/// Maps the map's landscape byte to a landscape type and provides the
/// terrain texture index (base image or border transition) for rendering.
/// </summary>
public static class LandscapeMapping
{
    /// <summary>Landscape images are stored in dat file 0.</summary>
    public const int LandscapeFile = 0;

    static readonly Dictionary<int, LandscapeType> originalLandscape = new()
    {
        [0] = LandscapeType.Water1,
        [1] = LandscapeType.Water2,
        [2] = LandscapeType.Water3,
        [3] = LandscapeType.Water4,
        [4] = LandscapeType.Water5,
        [5] = LandscapeType.Water6,
        [6] = LandscapeType.Water7,
        [7] = LandscapeType.Water8,
        [16] = LandscapeType.Grass,
        [17] = LandscapeType.MountainBorderOuter,
        [20] = LandscapeType.DesertBorderOuter,
        [21] = LandscapeType.MoorBorderOuter,
        [23] = LandscapeType.MudBorderOuter,
        [32] = LandscapeType.Mountain,
        [33] = LandscapeType.MountainBorder,
        [35] = LandscapeType.SnowBorderOuter,
        [48] = LandscapeType.Sand,
        [64] = LandscapeType.Desert,
        [65] = LandscapeType.DesertBorder,
        [80] = LandscapeType.Moor,
        [81] = LandscapeType.MoorBorder,
        [96] = LandscapeType.River1,
        [97] = LandscapeType.River2,
        [98] = LandscapeType.River3,
        [99] = LandscapeType.River4,
        [128] = LandscapeType.Snow,
        [129] = LandscapeType.SnowBorder,
        [144] = LandscapeType.Mud,
        [145] = LandscapeType.MudBorder
    };

    /// <summary>Base image number of each landscape type in the dat file.</summary>
    static readonly Dictionary<LandscapeType, int> imageNumbers = new()
    {
        [LandscapeType.Grass] = 0,
        [LandscapeType.DryGrass] = 1,
        [LandscapeType.Earth] = 2,
        [LandscapeType.Mud] = 4,
        [LandscapeType.MudBorder] = 5,
        [LandscapeType.MudBorderOuter] = 6,
        [LandscapeType.Moor] = 7,
        [LandscapeType.MoorBorder] = 8,
        [LandscapeType.MoorBorderOuter] = 9,
        [LandscapeType.Water8] = 10,
        [LandscapeType.Water7] = 11,
        [LandscapeType.Water6] = 12,
        [LandscapeType.Water5] = 13,
        [LandscapeType.Water4] = 14,
        [LandscapeType.Water3] = 15,
        [LandscapeType.Water2] = 16,
        [LandscapeType.Water1] = 17,
        [LandscapeType.River1] = 17,
        [LandscapeType.River2] = 17,
        [LandscapeType.River3] = 17,
        [LandscapeType.River4] = 17,
        [LandscapeType.Desert] = 18,
        [LandscapeType.DesertBorder] = 19,
        [LandscapeType.DesertBorderOuter] = 20,
        [LandscapeType.Mountain] = 21,
        [LandscapeType.MountainBorder] = 22,
        [LandscapeType.MountainBorderOuter] = 23,
        [LandscapeType.Snow] = 24,
        [LandscapeType.SnowBorder] = 25,
        [LandscapeType.SnowBorderOuter] = 26,
        [LandscapeType.Sand] = 31,
        [LandscapeType.Road] = 34,
        [LandscapeType.Flattened] = 35,
        [LandscapeType.DryEarth] = 36,
        [LandscapeType.FlattenedDesert] = 217,
        [LandscapeType.SharpFlattenedDesert] = 217,
        [LandscapeType.Gravel] = 230
    };

    readonly struct BorderTexture
    {
        public readonly LandscapeType Type1;
        public readonly LandscapeType Type1Alt;
        public readonly LandscapeType Type2;
        public readonly int BaseIndex;

        public BorderTexture(LandscapeType type1, LandscapeType type2, int baseIndex)
            : this(type1, type1, type2, baseIndex)
        {
        }

        public BorderTexture(LandscapeType type1, LandscapeType type1Alt, LandscapeType type2, int baseIndex)
        {
            Type1 = type1;
            Type1Alt = type1Alt;
            Type2 = type2;
            BaseIndex = baseIndex;
        }
    }

    static readonly BorderTexture[] borderTextures =
    {
        new(LandscapeType.Sand, LandscapeType.Water1, 37),

        new(LandscapeType.Grass, LandscapeType.Sand, LandscapeType.River1, 52),
        new(LandscapeType.Grass, LandscapeType.Sand, LandscapeType.River2, 56),
        new(LandscapeType.Grass, LandscapeType.Sand, LandscapeType.River3, 60),
        new(LandscapeType.Grass, LandscapeType.Sand, LandscapeType.River4, 64),

        new(LandscapeType.Sand, LandscapeType.River1, 68),
        new(LandscapeType.Sand, LandscapeType.River2, 72),
        new(LandscapeType.Sand, LandscapeType.River3, 76),
        new(LandscapeType.Sand, LandscapeType.River4, 80),

        new(LandscapeType.Water1, LandscapeType.Water2, 84),
        new(LandscapeType.Water2, LandscapeType.Water3, 88),
        new(LandscapeType.Water3, LandscapeType.Water4, 92),
        new(LandscapeType.Water4, LandscapeType.Water5, 96),
        new(LandscapeType.Water5, LandscapeType.Water6, 100),
        new(LandscapeType.Water6, LandscapeType.Water7, 104),
        new(LandscapeType.Water7, LandscapeType.Water8, 108),

        new(LandscapeType.Sand, LandscapeType.Grass, 112),

        new(LandscapeType.Grass, LandscapeType.MountainBorderOuter, 116),
        new(LandscapeType.MountainBorderOuter, LandscapeType.MountainBorder, 120),
        new(LandscapeType.Mountain, LandscapeType.MountainBorder, 124),

        new(LandscapeType.Grass, LandscapeType.DesertBorderOuter, 128),
        new(LandscapeType.DesertBorderOuter, LandscapeType.DesertBorder, 132),
        new(LandscapeType.Desert, LandscapeType.DesertBorder, 136),

        new(LandscapeType.Grass, LandscapeType.MudBorderOuter, 140),
        new(LandscapeType.MudBorderOuter, LandscapeType.MudBorder, 144),
        new(LandscapeType.Mud, LandscapeType.MudBorder, 148),

        new(LandscapeType.Mountain, LandscapeType.SnowBorderOuter, 156),
        new(LandscapeType.SnowBorderOuter, LandscapeType.SnowBorder, 160),
        new(LandscapeType.Snow, LandscapeType.SnowBorder, 164),

        // Some original maps have this.
        new(LandscapeType.Mountain, LandscapeType.Snow, 156),

        new(LandscapeType.Grass, LandscapeType.Earth, 168),
        new(LandscapeType.Grass, LandscapeType.Flattened, 172),

        new(LandscapeType.Grass, LandscapeType.Road, 185),

        new(LandscapeType.Grass, LandscapeType.DryGrass, 193),
        new(LandscapeType.Grass, LandscapeType.DryEarth, 197),

        new(LandscapeType.Grass, LandscapeType.MoorBorderOuter, 201),
        new(LandscapeType.MoorBorderOuter, LandscapeType.MoorBorder, 205),
        new(LandscapeType.Moor, LandscapeType.MoorBorder, 209),

        new(LandscapeType.Desert, LandscapeType.SharpFlattenedDesert, 218),
        new(LandscapeType.Desert, LandscapeType.FlattenedDesert, 222),

        new(LandscapeType.Mountain, LandscapeType.Gravel, 231)
    };

    /// <summary>Maps the map's landscape byte to a landscape type.</summary>
    public static LandscapeType? FromMapValue(byte value)
    {
        return originalLandscape.TryGetValue(value, out var type) ? type : null;
    }

    /// <summary>Gets the base image number of a landscape type.</summary>
    public static int GetImageNumber(LandscapeType type)
    {
        return imageNumbers.TryGetValue(type, out int number) ? number : 0;
    }

    /// <summary>
    /// Computes the terrain texture index for one triangle of a map cell.
    /// </summary>
    /// <param name="left">Landscape of the left edge cell.</param>
    /// <param name="center">Landscape of the center cell.</param>
    /// <param name="right">Landscape of the right edge cell.</param>
    /// <param name="useSecondParameter">Triangle variant (0 or 1).</param>
    public static int GetTextureIndex(LandscapeType left, LandscapeType center, LandscapeType right, int useSecondParameter)
    {
        if (center == left && center == right)
        {
            return GetImageNumber(center);
        }

        int textureIndex = GetImageNumber(left);
        foreach (var intersect in borderTextures)
        {
            int type1Count = 0;
            int type1AltCount = 0;
            int type2Count = 0;

            if (left == intersect.Type1) type1Count++;
            else if (left == intersect.Type1Alt) type1AltCount++;

            if (center == intersect.Type1) type1Count++;
            else if (center == intersect.Type1Alt) type1AltCount++;

            if (right == intersect.Type1) type1Count++;
            else if (right == intersect.Type1Alt) type1AltCount++;

            if (left == intersect.Type2) type2Count++;
            if (center == intersect.Type2) type2Count++;
            if (right == intersect.Type2) type2Count++;

            if (type1Count + type1AltCount + type2Count != 3 || type1AltCount == 2 || type2Count == 0)
                continue;

            textureIndex = intersect.BaseIndex;
            textureIndex += type2Count == 2 ? 2 : 0;
            textureIndex += useSecondParameter & 1;
            break;
        }

        return textureIndex;
    }
}
