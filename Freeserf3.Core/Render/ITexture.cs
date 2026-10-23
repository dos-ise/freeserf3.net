/*
 * ITexture.cs - Texture abstraction
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace Freeserf3.Render;

/// <summary>A GPU texture.</summary>
public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>Binds the texture to the given texture unit.</summary>
    void Bind(int unit = 0);
}

/// <summary>A region of a texture atlas.</summary>
public sealed class TextureRegion
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>Current atlas size (updated when the atlas grows).</summary>
    public int AtlasWidth { get; set; }
    public int AtlasHeight { get; set; }

    public TextureRegion(int x, int y, int width, int height, int atlasWidth, int atlasHeight)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
    }

    public float U0 => (float)X / AtlasWidth;
    public float V0 => (float)Y / AtlasHeight;
    public float U1 => (float)(X + Width) / AtlasWidth;
    public float V1 => (float)(Y + Height) / AtlasHeight;
}

/// <summary>A texture atlas that packs images and provides UV regions.</summary>
public interface ITextureAtlas : IDisposable
{
    ITexture Texture { get; }

    /// <summary>Adds an image (RGBA8) to the atlas and returns its region.</summary>
    TextureRegion AddImage(byte[] rgba, int width, int height);
}
