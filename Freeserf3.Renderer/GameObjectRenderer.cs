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

    /// <summary>Flag door sequence in the buildings file.</summary>
    public const int FlagSequence = 63;

    /// <summary>Road landscape image in the landscape file.</summary>
    public const int RoadImage = 34;

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
    readonly Dictionary<int, SpriteImage[]> settlerImages = new();
    SpriteImage? flagImage;
    SpriteImage? roadImage;

    public GameObjectRenderer(GL gl)
    {
        this.gl = gl;
        sprites = new SpriteRenderer(gl);
        atlas = new TextureAtlas(gl);
    }

    /// <summary>
    /// Loads the building/settler images. <paramref name="buildings"/> is
    /// dat file 13, <paramref name="settlers"/> the roman settler file (10).
    /// </summary>
    public void Load(DatFileReader buildings, DatFileReader settlers)
    {
        // Building sprites.
        foreach (var (type, sequence) in BuildingSequences)
        {
            if (buildingImages.ContainsKey(sequence))
                continue;

            var image = buildings.GetSettlerSequence(sequence).FirstOrDefault();
            if (image == null)
                continue;

            buildingImages[sequence] = AddToAtlas(image);
        }

        // Flag sprites (door).
        var flagDoor = buildings.GetSettlerSequence(FlagSequence).FirstOrDefault();
        if (flagDoor != null)
            flagImage = AddToAtlas(flagDoor);

        // Settler walking sequences (bearer, no material).
        for (int sequence = 0; sequence < 8; sequence++)
        {
            var frames = settlers.GetSettlerSequence(sequence);
            if (frames.Length == 0)
                continue;

            var images = new SpriteImage[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                images[i] = AddToAtlas(frames[i]);
            settlerImages[sequence] = images;
        }

        // Road texture (landscape image).
        var road = buildings.GetLandscape(RoadImage);
        if (road != null)
            roadImage = AddToAtlas(road);
    }

    SpriteImage AddToAtlas(DatImage image)
    {
        var region = atlas.AddImage(image.Rgba, image.Width, image.Height);
        return new SpriteImage(region, image.Width, image.Height, image.OffsetX, image.OffsetY);
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

        AddSprite(image, pos, height, 0f);
    }

    /// <summary>
    /// Adds a settler sprite at the given position. <paramref name="frame"/>
    /// selects the animation frame of the walking sequence.
    /// </summary>
    public void AddMovable(Position pos, int height, int frame = 0)
    {
        if (!settlerImages.TryGetValue(0, out var frames) || frames.Length == 0)
            return;

        AddSprite(frames[frame % frames.Length], pos, height, 0.5f);
    }

    /// <summary>Adds a road quad at the given grid position.</summary>
    public void AddRoad(Position pos, int height)
    {
        if (roadImage == null)
            return;

        AddSprite(roadImage.Value, pos, height, 0.1f);
    }

    /// <summary>Adds a flag sprite at the given grid position.</summary>
    public void AddFlag(Position pos, int height)
    {
        if (flagImage == null)
            return;

        AddSprite(flagImage.Value, pos, height, 0.2f);
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
