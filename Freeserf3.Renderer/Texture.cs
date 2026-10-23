/*
 * Texture.cs - OpenGL texture
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Render;
using Silk.NET.OpenGL;

namespace Freeserf3.Renderer;

public sealed class Texture : ITexture
{
    readonly GL gl;
    readonly uint handle;

    public int Width { get; }
    public int Height { get; }

    public Texture(GL gl, int width, int height, byte[]? rgba = null)
    {
        this.gl = gl;
        Width = width;
        Height = height;

        handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);

        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    }

    public void Update(byte[] rgba)
    {
        gl.BindTexture(TextureTarget.Texture2D, handle);
        gl.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, 0, 0,
            (uint)Width, (uint)Height, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
    }

    /// <summary>Uploads a region of the texture (used by the atlas).</summary>
    public void UpdateRegion(byte[] rgba, int x, int y, int width, int height)
    {
        gl.BindTexture(TextureTarget.Texture2D, handle);
        gl.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, x, y,
            (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
    }

    public void Bind(int unit = 0)
    {
        gl.ActiveTexture(TextureUnit.Texture0 + unit);
        gl.BindTexture(TextureTarget.Texture2D, handle);
    }

    public void Dispose()
    {
        gl.DeleteTexture(handle);
    }
}
