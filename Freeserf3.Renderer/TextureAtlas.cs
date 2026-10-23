/*
 * TextureAtlas.cs - Simple shelf-packing texture atlas
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Render;
using Silk.NET.OpenGL;

namespace Freeserf3.Renderer;

/// <summary>
/// A texture atlas that packs RGBA images into a single texture using a
/// simple shelf (row) packing algorithm. Only the changed region is uploaded
/// to the GPU per image.
/// </summary>
public sealed class TextureAtlas : ITextureAtlas
{
    const int InitialSize = 2048;
    const int MaxSize = 8192;
    const int Padding = 1;

    readonly GL gl;
    readonly List<TextureRegion> regions = new();
    Texture texture;
    byte[] pixels;
    int shelfX;
    int shelfY;
    int shelfHeight;

    public ITexture Texture => texture;

    public TextureAtlas(GL gl)
    {
        this.gl = gl;
        pixels = new byte[InitialSize * InitialSize * 4];
        texture = new Texture(gl, InitialSize, InitialSize, pixels);
    }

    public TextureRegion AddImage(byte[] rgba, int width, int height)
    {
        int paddedWidth = width + 2 * Padding;
        int paddedHeight = height + 2 * Padding;

        if (shelfX + paddedWidth > texture.Width)
        {
            // Move to next shelf.
            shelfX = 0;
            shelfY += shelfHeight;
            shelfHeight = 0;
        }

        if (shelfY + paddedHeight > texture.Height)
        {
            Grow();
        }

        int x = shelfX + Padding;
        int y = shelfY + Padding;

        // Copy the image into the atlas pixel buffer.
        for (int row = 0; row < height; row++)
        {
            int srcOffset = row * width * 4;
            int dstOffset = ((y + row) * texture.Width + x) * 4;
            Array.Copy(rgba, srcOffset, pixels, dstOffset, width * 4);
        }

        shelfX += paddedWidth;
        shelfHeight = Math.Max(shelfHeight, paddedHeight);

        // Upload only the changed region.
        texture.UpdateRegion(rgba, x, y, width, height);

        var region = new TextureRegion(x, y, width, height, texture.Width, texture.Height);
        regions.Add(region);
        return region;
    }

    void Grow()
    {
        int newSize = texture.Width * 2;
        if (newSize > MaxSize)
            throw new InvalidOperationException("Texture atlas exceeded maximum size.");

        Console.WriteLine($"TextureAtlas: growing {texture.Width} -> {newSize}");

        var newPixels = new byte[newSize * newSize * 4];
        for (int row = 0; row < texture.Height; row++)
        {
            Array.Copy(pixels, row * texture.Width * 4, newPixels, row * newSize * 4, texture.Width * 4);
        }

        pixels = newPixels;
        texture.Dispose();
        texture = new Texture(gl, newSize, newSize, pixels);

        // The atlas size changed: update all existing regions so their UVs
        // stay correct.
        foreach (var region in regions)
        {
            region.AtlasWidth = newSize;
            region.AtlasHeight = newSize;
        }
    }

    public void Dispose()
    {
        texture.Dispose();
    }
}
