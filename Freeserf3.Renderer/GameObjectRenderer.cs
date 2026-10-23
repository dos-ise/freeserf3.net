/*
 * GameObjectRenderer.cs - Renders buildings, movables, roads and flags
 *
 * The building sprite sequences follow the settlers-remake (MIT) building
 * XML files: buildings are stored in dat file 13, each building type has a
 * sequence index. Settlers use the roman settler file (10) and flags the
 * flag sequence (63) of file 13.
 *
 * Sprite positioning follows the settlers-remake geometry: the sprite's
 * top-left is at (anchorX + offsetX, anchorY + offsetY) and it extends
 * down by its height, where the anchor is the grid position converted to
 * map pixel space.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Render;
using Freeserf3.Simulation;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>A sprite image: texture region plus dimensions and anchor offset.</summary>
public readonly struct SpriteImage
{
    public SpriteImage(TextureRegion region, int width, int height, int offsetX, int offsetY)
    {
        Region = region;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public TextureRegion Region { get; }
    public int Width { get; }
    public int Height { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
}

/// <summary>
/// Draws the game objects (buildings, movables, roads, flags) on top of
/// the terrain. Sprites are collected per frame and drawn in z-order.
/// </summary>
public sealed class GameObjectRenderer : IDisposable
{
    /// <summary>Buildings and flags are stored in dat file 13.</summary>
    public const int BuildingsFile = 13;

    /// <summary>Roman settler sprites are stored in dat file 10.</summary>
    public const int SettlersFile = 10;

    /// <summary>Map objects (trees, stones, ...) are stored in dat file 1.</summary>
    public const int ObjectsFile = 1;

    /// <summary>Flag door sequence in the buildings file.</summary>
    public const int FlagSequence = 63;

    /// <summary>Border post sequence in the buildings file.</summary>
    public const int BorderSequence = 65;

    /// <summary>Road landscape image in the landscape file.</summary>
    public const int RoadImage = 34;

    /// <summary>Tree sprite sequences in the objects file (from settlers-remake).</summary>
    public static readonly int[] TreeSequences = { 1, 2, 4, 7, 8, 16, 17 };

    /// <summary>Stone sprite sequence in the objects file (from settlers-remake MapObjectDrawer).</summary>
    public const int StoneSequence = 31;

    /// <summary>Stonecutter working sequence (ACTION1) in the settler file (from settlers-remake movables.txt).</summary>
    public const int StonecutterWorkSequence = 25;

    /// <summary>Lumberjack working sequence (ACTION1) in the settler file (from settlers-remake movables.txt).</summary>
    public const int LumberjackWorkSequence = 30;

    /// <summary>Forester working sequence (ACTION1) in the settler file (from settlers-remake movables.txt).</summary>
    public const int ForesterWorkSequence = 36;

    /// <summary>Stack sprite sequence per material in the objects file (from settlers-remake EMaterialType).</summary>
    public static readonly Dictionary<Material, int> StackSequences = new()
    {
        [Material.Plank] = 33,
        [Material.Stone] = 43,
        [Material.Trunk] = 41,
        [Material.Bread] = 49,
        [Material.Coal] = 34,
        [Material.GoldOre] = 36,
        [Material.IronOre] = 39,
        [Material.Fish] = 47,
        [Material.Crop] = 50,
        [Material.Gold] = 37,
        [Material.Iron] = 42,
        [Material.Blade] = 55,
        [Material.Hammer] = 51,
        [Material.Axe] = 46,
        [Material.Pick] = 53,
        [Material.Saw] = 54,
        [Material.FishingRod] = 66,
        [Material.Sword] = 59,
        [Material.Bow] = 63,
        [Material.Spear] = 60,
        [Material.Wine] = 69,
        [Material.Flour] = 48,
        [Material.Pig] = 73,
        [Material.Meat] = 52,
        [Material.Sulfur] = 80,
        [Material.Water] = 77,
        [Material.Rice] = 78,
        [Material.Gems] = 79,
        [Material.Keg] = 70,
        [Material.Scythe] = 56,
        [Material.Honey] = 89
    };

    /// <summary>
    /// Carrier sprite sequence per material in the settler file (dat 10).
    /// Sequence 0 is the empty carrier, sequences 1-12 show the carrier
    /// carrying a good on his back. The mapping is a best guess from the
    /// sprite colors and the original material order; refine by checking
    /// the game on the device.
    /// </summary>
    public static readonly Dictionary<Material, int> CarrierSequences = new()
    {
        [Material.Plank] = 1,
        [Material.Stone] = 2,
        [Material.Pig] = 3,
        [Material.Trunk] = 4,
        [Material.Bread] = 5,
        [Material.Coal] = 6,
        [Material.GoldOre] = 7,
        [Material.IronOre] = 8,
        [Material.Fish] = 9,
        [Material.Meat] = 10,
        [Material.Crop] = 11,
        [Material.Water] = 12
    };

    /// <summary>Building sprite sequence per building type (from the building XMLs).</summary>
    public static readonly Dictionary<BuildingType, int> BuildingSequences = new()
    {
        [BuildingType.Lumberjack] = 0,
        [BuildingType.Sawmill] = 1,
        [BuildingType.LookoutTower] = 2,
        [BuildingType.Stonecutter] = 3,
        [BuildingType.Ironmine] = 4,
        [BuildingType.Goldmine] = 5,
        [BuildingType.Coalmine] = 6,
        [BuildingType.Forester] = 7,
        [BuildingType.Goldmelt] = 8,
        [BuildingType.Ironmelt] = 9,
        [BuildingType.Toolsmith] = 10,
        [BuildingType.Weaponsmith] = 11,
        [BuildingType.Stock] = 12,
        [BuildingType.Slaughterhouse] = 13,
        [BuildingType.Mill] = 14,
        [BuildingType.Castle] = 16,
        [BuildingType.Tower] = 17,
        [BuildingType.BigTower] = 18,
        [BuildingType.Waterworks] = 19,
        [BuildingType.PigFarm] = 20,
        [BuildingType.Farm] = 21,
        [BuildingType.Fisher] = 22,
        [BuildingType.Baker] = 23,
        [BuildingType.SmallLivinghouse] = 24,
        [BuildingType.MediumLivinghouse] = 25,
        [BuildingType.BigLivinghouse] = 26,
        [BuildingType.MarketPlace] = 28,
        [BuildingType.Hospital] = 29,
        [BuildingType.CharcoalBurner] = 30,
        [BuildingType.Temple] = 31,
        [BuildingType.BigTemple] = 32,
        [BuildingType.Winegrower] = 33,
        [BuildingType.Barrack] = 34,
        [BuildingType.DonkeyFarm] = 35,
        [BuildingType.Dockyard] = 44,
        [BuildingType.Harbor] = 45,
        [BuildingType.Laboratory] = 0,
        [BuildingType.Gemsmine] = 1,
        [BuildingType.Sulfurmine] = 2,
        [BuildingType.Beekeeping] = 3,
        [BuildingType.MeadBrewery] = 4,
        [BuildingType.Distillery] = 22,
        [BuildingType.RiceFarm] = 29,
        [BuildingType.Brewery] = 34
    };

    readonly GL gl;
    readonly SpriteRenderer sprites;
    readonly TextureAtlas atlas;
    readonly Dictionary<int, SpriteImage> buildingImages = new();
    readonly Dictionary<int, SpriteImage> constructionImages = new();
    readonly Dictionary<(int Color, int Sequence), SpriteImage[]> settlerImages = new();
    readonly Dictionary<int, SpriteImage[]> treeImages = new();
    readonly Dictionary<int, SpriteImage> borderImages = new();
    readonly Dictionary<int, SpriteImage[]> stackImages = new();
    SpriteImage[]? stoneImages;
    SpriteImage? flagImage;
    SpriteImage? roadImage;
    TextureRegion whiteRegion;

    // Original settler colors (from settlers-remake MapDrawContext).
    public static readonly (byte R, byte G, byte B)[] PlayerColors =
    {
        (0xF7, 0x10, 0x00), (0x10, 0x8C, 0xF7), (0xF7, 0xF7, 0x00), (0x29, 0xB5, 0x52),
        (0xF7, 0x8C, 0x00), (0x00, 0xF7, 0xF7), (0xF7, 0x00, 0xF7), (0x29, 0x29, 0x29),
        (0xF7, 0xF7, 0xF7), (0x00, 0x10, 0xF7), (0xCE, 0x4A, 0x10), (0x8C, 0x8C, 0x8C),
        (0xAD, 0x08, 0xDE), (0x00, 0x6B, 0x00), (0xF7, 0xBD, 0xBD), (0x84, 0xEF, 0xA5),
        (0x9C, 0x08, 0x31), (0xCE, 0x8C, 0xE7), (0xF7, 0xCE, 0x94), (0x8C, 0xBD, 0xEF),
        (0xBA, 0xFF, 0x45), (0xCD, 0x09, 0x73), (0xD9, 0xF1, 0xAF), (0x6E, 0x00, 0x5F),
        (0xA3, 0xC5, 0x03), (0x64, 0xB3, 0xB9), (0xB3, 0xF6, 0xFB), (0x8E, 0x59, 0x2B),
        (0x8E, 0x88, 0x2B), (0xD9, 0xE0, 0xFF), (0xD4, 0xD4, 0xD4), (0xFF, 0x57, 0x8F)
    };

    public GameObjectRenderer(GL gl)
    {
        this.gl = gl;
        sprites = new SpriteRenderer(gl);
        atlas = new TextureAtlas(gl);
        whiteRegion = atlas.AddImage(new byte[] { 255, 255, 255, 255 }, 1, 1);
    }

    /// <summary>
    /// Loads the building/settler images. <paramref name="buildings"/> is
    /// dat file 13, <paramref name="settlers"/> the roman settler file (10).
    /// <paramref name="playerColors"/> lists the player color indices that
    /// appear on the map; only those settler tints are generated.
    /// </summary>
    public void Load(DatFileReader buildings, DatFileReader settlers, IEnumerable<int> playerColors)
    {
        // Building sprites. Image 0 is the finished building, image 1 the
        // construction site (from the building XMLs: for="FINAL" / for="BUILD").
        foreach (var (type, sequence) in BuildingSequences)
        {
            if (buildingImages.ContainsKey(sequence))
                continue;

            var frames = buildings.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            buildingImages[sequence] = AddToAtlas(frames[0]);
            if (frames.Length > 1)
                constructionImages[sequence] = AddToAtlas(frames[1]);
        }

        // Flag sprites (door).
        var flagDoor = buildings.GetSettlerSequence(FlagSequence).FirstOrDefault();
        if (flagDoor != null)
            flagImage = AddToAtlas(flagDoor);

        // Settler walking sequences (bearer, no material), tinted per player
        // color: the torso (color mask) is combined with the main image and
        // the shadow.
        var colors = playerColors.Distinct().Where(c => c >= 0 && c < PlayerColors.Length).ToList();
        if (colors.Count == 0)
            colors.Add(0);

        for (int sequence = 0; sequence < 8; sequence++)
        {
            var frames = settlers.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            var torsos = settlers.GetSettlerTorsoSequence(sequence);
            var shadows = settlers.GetSettlerShadowSequence(sequence);

            foreach (int color in colors)
            {
                var images = new SpriteImage[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    var torso = i < torsos.Length ? torsos[i] : null;
                    var shadow = i < shadows.Length ? shadows[i] : null;
                    images[i] = AddToAtlas(CombineSettler(frames[i], torso, shadow, PlayerColors[color]));
                }
                settlerImages[(color, sequence)] = images;
            }
        }

        // Working sequences of the resource workers (from settlers-remake
        // movables.txt): stonecutter ACTION1 = 25, lumberjack ACTION1 = 30,
        // forester ACTION1 = 36.
        foreach (int sequence in new[] { StonecutterWorkSequence, LumberjackWorkSequence, ForesterWorkSequence })
        {
            var frames = settlers.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            var torsos = settlers.GetSettlerTorsoSequence(sequence);
            var shadows = settlers.GetSettlerShadowSequence(sequence);

            foreach (int color in colors)
            {
                var images = new SpriteImage[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    var torso = i < torsos.Length ? torsos[i] : null;
                    var shadow = i < shadows.Length ? shadows[i] : null;
                    images[i] = AddToAtlas(CombineSettler(frames[i], torso, shadow, PlayerColors[color]));
                }
                settlerImages[(color, sequence)] = images;
            }
        }

        // Road texture (landscape image).
        var road = buildings.GetLandscape(RoadImage);
        if (road != null)
            roadImage = AddToAtlas(road);
    }

    /// <summary>
    /// Loads the map object images (trees) from dat file 1 and the border
    /// posts (tinted per player color) from the buildings file.
    /// </summary>
    public void LoadObjects(DatFileReader objects, DatFileReader buildings, IEnumerable<int> playerColors)
    {
        // Trees: one animated sequence per tree type.
        foreach (int sequence in TreeSequences)
        {
            var frames = objects.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            var images = new SpriteImage[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                images[i] = AddToAtlas(frames[i]);
            treeImages[sequence] = images;
        }
        Console.WriteLine($"Trees: {treeImages.Count} sequences loaded");

        // Stones: one sequence with a frame per remaining stone count.
        var stoneFrames = objects.GetSettlerSequence(StoneSequence);
        if (stoneFrames.Length > 0)
        {
            stoneImages = new SpriteImage[stoneFrames.Length];
            for (int i = 0; i < stoneFrames.Length; i++)
                stoneImages[i] = AddToAtlas(stoneFrames[i]);
        }
        Console.WriteLine($"Stones: {stoneImages?.Length ?? 0} frames loaded");

        // Border posts tinted with the player colors.
        var borderFrames = buildings.GetSettlerSequence(BorderSequence);
        if (borderFrames.Length > 0)
        {
            var colors = playerColors.Distinct().Where(c => c >= 0 && c < PlayerColors.Length).ToList();
            if (colors.Count == 0)
                colors.Add(0);

            foreach (int color in colors)
            {
                borderImages[color] = AddToAtlas(TintImage(borderFrames[0], PlayerColors[color]));
            }
        }
        Console.WriteLine($"Borders: {borderImages.Count} colors loaded");

        // Resource stack images (one sequence per material, one frame per
        // stack size).
        foreach (int sequence in StackSequences.Values.Distinct())
        {
            var frames = objects.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            var images = new SpriteImage[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                images[i] = AddToAtlas(frames[i]);
            stackImages[sequence] = images;
        }
        Console.WriteLine($"Stacks: {stackImages.Count} sequences loaded");
    }

    // Tints a grayscale image with the given color (used for border posts).
    static DatImage TintImage(DatImage image, (byte R, byte G, byte B) color)
    {
        var rgba = new byte[image.Rgba.Length];
        for (int i = 0; i < image.Rgba.Length; i += 4)
        {
            byte a = image.Rgba[i + 3];
            if (a == 0)
                continue;
            // Use the blue channel as the tint intensity.
            int intensity = image.Rgba[i + 2];
            rgba[i] = (byte)(color.R * intensity / 255);
            rgba[i + 1] = (byte)(color.G * intensity / 255);
            rgba[i + 2] = (byte)(color.B * intensity / 255);
            rgba[i + 3] = a;
        }
        return new DatImage(image.Width, image.Height, image.OffsetX, image.OffsetY, rgba);
    }

    SpriteImage AddToAtlas(DatImage image)
    {
        var region = atlas.AddImage(image.Rgba, image.Width, image.Height);
        return new SpriteImage(region, image.Width, image.Height, image.OffsetX, image.OffsetY);
    }

    // Combines a settler frame: the torso (color mask) tinted with the
    // player color, the main image on top and the shadow below, into a
    // single RGBA image (mirrors settlers-remake SettlerImage).
    static DatImage CombineSettler(DatImage image, DatImage? torso, DatImage? shadow, (byte R, byte G, byte B) color)
    {
        int minX = image.OffsetX;
        int minY = image.OffsetY;
        int maxX = image.OffsetX + image.Width;
        int maxY = image.OffsetY + image.Height;

        if (torso != null)
        {
            minX = Math.Min(minX, torso.OffsetX);
            minY = Math.Min(minY, torso.OffsetY);
            maxX = Math.Max(maxX, torso.OffsetX + torso.Width);
            maxY = Math.Max(maxY, torso.OffsetY + torso.Height);
        }
        if (shadow != null)
        {
            minX = Math.Min(minX, shadow.OffsetX);
            minY = Math.Min(minY, shadow.OffsetY);
            maxX = Math.Max(maxX, shadow.OffsetX + shadow.Width);
            maxY = Math.Max(maxY, shadow.OffsetY + shadow.Height);
        }

        int width = maxX - minX;
        int height = maxY - minY;
        var rgba = new byte[width * height * 4];

        void Blend(int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || a == 0)
                return;
            int index = (y * width + x) * 4;
            if (a == 255)
            {
                rgba[index] = r;
                rgba[index + 1] = g;
                rgba[index + 2] = b;
                rgba[index + 3] = 255;
            }
            else
            {
                // Alpha blend over the existing pixel.
                int dstA = rgba[index + 3];
                int outA = a + dstA * (255 - a) / 255;
                if (outA == 0)
                    return;
                rgba[index] = (byte)((r * a + rgba[index] * dstA * (255 - a) / 255) / outA);
                rgba[index + 1] = (byte)((g * a + rgba[index + 1] * dstA * (255 - a) / 255) / outA);
                rgba[index + 2] = (byte)((b * a + rgba[index + 2] * dstA * (255 - a) / 255) / outA);
                rgba[index + 3] = (byte)outA;
            }
        }

        // Shadow (semi-transparent black).
        if (shadow != null)
        {
            for (int y = 0; y < shadow.Height; y++)
            {
                for (int x = 0; x < shadow.Width; x++)
                {
                    int si = (y * shadow.Width + x) * 4;
                    byte a = shadow.Rgba[si + 3];
                    if (a == 0)
                        continue;
                    Blend(shadow.OffsetX - minX + x, shadow.OffsetY - minY + y, 0, 0, 0, a);
                }
            }
        }

        // Torso tinted with the player color (the torso is a grayscale mask).
        if (torso != null)
        {
            for (int y = 0; y < torso.Height; y++)
            {
                for (int x = 0; x < torso.Width; x++)
                {
                    int si = (y * torso.Width + x) * 4;
                    byte a = torso.Rgba[si + 3];
                    if (a == 0)
                        continue;
                    // Use the blue channel as the tint intensity.
                    int intensity = torso.Rgba[si + 2];
                    Blend(torso.OffsetX - minX + x, torso.OffsetY - minY + y,
                        (byte)(color.R * intensity / 255), (byte)(color.G * intensity / 255), (byte)(color.B * intensity / 255), a);
                }
            }
        }

        // Main image on top.
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int si = (y * image.Width + x) * 4;
                byte a = image.Rgba[si + 3];
                if (a == 0)
                    continue;
                Blend(image.OffsetX - minX + x, image.OffsetY - minY + y,
                    image.Rgba[si], image.Rgba[si + 1], image.Rgba[si + 2], a);
            }
        }

        return new DatImage(width, height, minX, minY, rgba);
    }

    /// <summary>Clears the sprite list for a new frame.</summary>
    public void Begin()
    {
        sprites.Begin();
    }

    /// <summary>Adds a building sprite at the given grid position.</summary>
    public void AddBuilding(BuildingType type, Position pos, int height)
    {
        if (!BuildingSequences.TryGetValue(type, out int sequence))
            return;
        if (!buildingImages.TryGetValue(sequence, out var image))
            return;

        AddSprite(image, pos, height, 0.2f);
    }

    /// <summary>Adds a construction site sprite at the given grid position.</summary>
    public void AddConstructionSite(BuildingType type, Position pos, int height)
    {
        if (!BuildingSequences.TryGetValue(type, out int sequence))
            return;
        if (!constructionImages.TryGetValue(sequence, out var image))
            return;

        AddSprite(image, pos, height, 0.2f);
    }

    /// <summary>
    /// Adds a semi-transparent building preview (ghost) at the given
    /// position. Green when the placement is valid, red otherwise.
    /// </summary>
    public void AddBuildingGhost(BuildingType type, Position pos, int height, bool valid)
    {
        if (!BuildingSequences.TryGetValue(type, out int sequence))
            return;
        if (!buildingImages.TryGetValue(sequence, out var image))
            return;

        var (mapX, mapY) = MapGeometry.GridToScreen(pos, height);
        float x = mapX + image.OffsetX;
        float y = mapY + image.OffsetY;
        var color = valid ? new Vector4(0.3f, 1f, 0.3f, 0.5f) : new Vector4(1f, 0.3f, 0.3f, 0.5f);
        sprites.Add(new Sprite(image.Region, x, y, image.Width, image.Height, 0.2f, color));
    }

    /// <summary>
    /// Adds a settler sprite at the given position. <paramref name="frame"/>
    /// selects the walking animation step (0-10) and <paramref name="direction"/>
    /// the facing direction of the walking sequence.
    /// </summary>
    public void AddMovable(Position pos, int height, int playerColor, int frame, Direction direction = Direction.SouthWest)
    {
        if (!settlerImages.TryGetValue((playerColor, 0), out var frames) || frames.Length == 0)
            return;

        AddSprite(frames[DirectionFrameIndex(direction, frame)], pos, height, 0.5f);
    }

    /// <summary>
    /// Adds a resource worker sprite while it is cutting a tree or mining a
    /// stone. Uses the working sequence of the profession (lumberjack
    /// ACTION1, stonecutter ACTION1). The working sequences hold fewer
    /// frames than the walking ones, so the animation step is clamped.
    /// </summary>
    public void AddWorkingMovable(Position pos, int height, int playerColor, int frame, Direction direction, WorkerType profession)
    {
        int sequence = profession switch
        {
            WorkerType.Stonecutter => StonecutterWorkSequence,
            WorkerType.Forester => ForesterWorkSequence,
            _ => LumberjackWorkSequence
        };
        if (!settlerImages.TryGetValue((playerColor, sequence), out var frames) || frames.Length == 0)
            return;

        int index = Math.Clamp(frame, 0, frames.Length - 1);
        AddSprite(frames[index], pos, height, 0.5f);
    }

    /// <summary>
    /// Adds a carrier sprite. When the carrier carries a good with a
    /// dedicated carrying animation (sequence 1-12, good on the back) that
    /// sequence is used, otherwise the plain walking animation (sequence 0).
    /// </summary>
    public void AddCarrier(Position pos, int height, int playerColor, int frame, Material? carried, Direction direction = Direction.SouthWest)
    {
        int sequence = carried.HasValue && CarrierSequences.TryGetValue(carried.Value, out int s) ? s : 0;
        if (!settlerImages.TryGetValue((playerColor, sequence), out var frames) || frames.Length == 0)
            return;

        AddSprite(frames[DirectionFrameIndex(direction, frame)], pos, height, 0.5f);
    }

    /// <summary>
    /// Frame index of the walking animation for the given direction. Each
    /// settler sequence holds 6 directions x 12 frames (1 idle + 11 walk).
    /// </summary>
    static int DirectionFrameIndex(Direction direction, int frame)
    {
        int offset = ((int)direction + 3) % 6 * 12;
        return offset + 1 + (frame % 11);
    }

    /// <summary>Adds a road quad at the given grid position.</summary>
    public void AddRoad(Position pos, int height)
    {
        if (roadImage == null)
            return;

        AddSprite(roadImage.Value, pos, height, 0f);
    }

    /// <summary>Adds a flag sprite at the given grid position.</summary>
    public void AddFlag(Position pos, int height)
    {
        if (flagImage == null)
            return;

        AddSprite(flagImage.Value, pos, height, 0.1f);
    }

    /// <summary>
    /// Adds a tree sprite at the given grid position. The tree type is
    /// derived from the position (like settlers-remake) and the animation
    /// frame from the game clock.
    /// </summary>
    public void AddTree(Position pos, int height, ulong tick)
    {
        if (treeImages.Count == 0)
            return;

        int treeType = (pos.X + pos.X / 5 + pos.Y / 3 + pos.Y + pos.Y / 7) % TreeSequences.Length;
        int sequence = TreeSequences[treeType];
        if (!treeImages.TryGetValue(sequence, out var frames) || frames.Length == 0)
            return;

        int step = (int)((tick + (ulong)(pos.X * 167 + pos.Y * 1223)) % (ulong)frames.Length);
        AddSprite(frames[step], pos, height, 0.3f);
    }

    /// <summary>
    /// Adds a stone sprite at the given grid position. The frame shows the
    /// remaining stone count (like settlers-remake drawStones).
    /// </summary>
    public void AddStone(Position pos, int height, int remaining)
    {
        if (stoneImages == null || stoneImages.Length == 0)
            return;

        int frame = Math.Clamp(stoneImages.Length - remaining - 1, 0, stoneImages.Length - 1);
        AddSprite(stoneImages[frame], pos, height, 0.3f);
    }

    /// <summary>Adds a player border post at the given grid position.</summary>
    public void AddBorder(Position pos, int height, int playerColor)
    {
        if (!borderImages.TryGetValue(playerColor, out var image))
            return;

        AddSprite(image, pos, height, 0.05f);
    }

    /// <summary>
    /// Adds a resource stack sprite at the given grid position. The frame
    /// shows the number of goods on the stack.
    /// </summary>
    public void AddStack(Position pos, int height, Material material, int count)
    {
        if (!StackSequences.TryGetValue(material, out int sequence))
            return;
        if (!stackImages.TryGetValue(sequence, out var frames) || frames.Length == 0)
            return;

        int frame = Math.Clamp(count - 1, 0, frames.Length - 1);
        AddSprite(frames[frame], pos, height, 0.25f);
    }

    /// <summary>
    /// Adds a small carried-good indicator above the given position (used
    /// for settlers carrying a resource).
    /// </summary>
    public void AddCarriedGood(Position pos, int height, Material material)
    {
        if (!StackSequences.TryGetValue(material, out int sequence))
            return;
        if (!stackImages.TryGetValue(sequence, out var frames) || frames.Length == 0)
            return;

        var image = frames[0];
        var (mapX, mapY) = MapGeometry.GridToScreen(pos, height);
        // Draw a small version of the good above the settler.
        float w = image.Width * 0.5f;
        float h = image.Height * 0.5f;
        sprites.Add(image.Region, mapX + image.OffsetX * 0.5f, mapY + image.OffsetY * 0.5f - 20f, w, h, 0.6f);
    }

    /// <summary>
    /// Whether a map-file object byte is a tree. Tree objects are 68-80
    /// (birch, elm, oak, ...) and 84 (small tree), see the original map
    /// object types.
    /// </summary>
    public static bool IsTreeObject(byte mapObject)
    {
        return mapObject is >= 68 and <= 80 or 84;
    }

    /// <summary>Adds a selection marker (semi-transparent quad) at the given position.</summary>
    public void AddSelectionMarker(Position pos, int height)
    {
        var (mapX, mapY) = MapGeometry.GridToScreen(pos, height);
        // A marker quad covering the cell, slightly above the terrain.
        sprites.Add(new Sprite(whiteRegion, mapX - 8f, mapY - 8f, 16f, 16f, 0.9f, new Vector4(1f, 0.9f, 0.2f, 0.5f)));
    }

    void AddSprite(in SpriteImage image, Position pos, int height, float z)
    {
        var (mapX, mapY) = MapGeometry.GridToScreen(pos, height);
        float x = mapX + image.OffsetX;
        float y = mapY + image.OffsetY;
        sprites.Add(image.Region, x, y, image.Width, image.Height, z);
    }

    /// <summary>Draws all collected sprites.</summary>
    public void Draw(in Matrix4x4 viewProjection, float zoom)
    {
        atlas.Texture.Bind(0);
        sprites.Draw(viewProjection, zoom);
    }

    public void Dispose()
    {
        sprites.Dispose();
        atlas.Dispose();
    }
}
