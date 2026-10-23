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
public readonly struct TextureRegion
{
    public float U0 { get; }
    public float V0 { get; }
    public float U1 { get; }
    public float V1 { get; }

    public TextureRegion(float u0, float v0, float u1, float v1)
    {
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
    }
}

/// <summary>A texture atlas that packs images and provides UV regions.</summary>
public interface ITextureAtlas : IDisposable
{
    ITexture Texture { get; }

    /// <summary>Adds an image (RGBA8) to the atlas and returns its region.</summary>
    TextureRegion AddImage(byte[] rgba, int width, int height);
}
