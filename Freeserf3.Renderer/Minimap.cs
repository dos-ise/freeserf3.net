/*
 * Minimap.cs - A small overview of the whole map
 *
 * The minimap is a texture with one pixel per map cell, colored by the
 * landscape type. It is drawn as a quad in the top-right corner of the
 * screen.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Simulation;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>Renders a small overview of the map in a screen corner.</summary>
public sealed class Minimap : IDisposable
{
    readonly GL gl;
    readonly Texture texture;
    readonly ShaderProgram shader;
    readonly uint vao;
    readonly uint vbo;
    readonly int size;

    public Minimap(GL gl, Map map)
    {
        this.gl = gl;
        size = map.Size;

        texture = new Texture(gl, size, size, BuildPixels(map));

        shader = CreateShader(gl);

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        const uint stride = 4 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        gl.BindVertexArray(0);
    }

    /// <summary>Builds the minimap pixels (one per cell, colored by landscape).</summary>
    static byte[] BuildPixels(Map map)
    {
        var pixels = new byte[map.Size * map.Size * 4];

        for (int y = 0; y < map.Size; y++)
        {
            for (int x = 0; x < map.Size; x++)
            {
                var cell = map.GetCell(x, y);
                var (r, g, b) = GetLandscapeColor(cell.Landscape);
                int index = (y * map.Size + x) * 4;
                pixels[index] = r;
                pixels[index + 1] = g;
                pixels[index + 2] = b;
                pixels[index + 3] = 255;
            }
        }

        return pixels;
    }

    static (byte R, byte G, byte B) GetLandscapeColor(byte landscape)
    {
        var type = LandscapeMapping.FromMapValue(landscape);
        return type switch
        {
            LandscapeType.Water1 or LandscapeType.Water2 or LandscapeType.Water3
                or LandscapeType.Water4 or LandscapeType.Water5 or LandscapeType.Water6
                or LandscapeType.Water7 or LandscapeType.Water8 => (40, 90, 200),
            LandscapeType.River1 or LandscapeType.River2 or LandscapeType.River3
                or LandscapeType.River4 => (60, 120, 220),
            LandscapeType.Mountain or LandscapeType.MountainBorder
                or LandscapeType.MountainBorderOuter => (120, 120, 120),
            LandscapeType.Snow or LandscapeType.SnowBorder or LandscapeType.SnowBorderOuter => (240, 240, 240),
            LandscapeType.Sand => (230, 210, 140),
            LandscapeType.Desert or LandscapeType.DesertBorder or LandscapeType.DesertBorderOuter => (220, 190, 120),
            LandscapeType.Moor or LandscapeType.MoorBorder or LandscapeType.MoorBorderOuter => (90, 120, 80),
            LandscapeType.Mud or LandscapeType.MudBorder or LandscapeType.MudBorderOuter => (140, 110, 80),
            _ => (90, 160, 70) // grass and everything else
        };
    }

    /// <summary>Draws the minimap inside the given screen rectangle.</summary>
    public void Draw(float x, float y, float width, float height)
    {
        float x0 = x;
        float y0 = y;
        float x1 = x + width;
        float y1 = y + height;

        float[] vertices =
        {
            x0, y0, 0f, 0f,
            x1, y0, 1f, 0f,
            x1, y1, 1f, 1f,
            x0, y0, 0f, 0f,
            x1, y1, 1f, 1f,
            x0, y1, 0f, 1f
        };

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);

        shader.Use();
        shader.SetUniformMatrix4("uProjection", Matrix4x4.CreateOrthographicOffCenter(0, x1, y1, 0, -1, 1));

        texture.Bind(0);
        shader.SetUniform1("uTexture", 0);

        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
    }

    static ShaderProgram CreateShader(GL gl)
    {
        string version = State.GetShaderVersionDirective();
        string precision = State.IsOpenGLES ? "precision mediump float;\n" : "";

        string vertexSource = $@"{version}
in vec2 aPosition;
in vec2 aUv;

uniform mat4 uProjection;

out vec2 vUv;

void main()
{{
    gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
    vUv = aUv;
}}
";

        string fragmentSource = $@"{version}
{precision}in vec2 vUv;

uniform sampler2D uTexture;

out vec4 fragColor;

void main()
{{
    fragColor = texture(uTexture, vUv);
}}
";

        return new ShaderProgram(gl, vertexSource, fragmentSource);
    }

    public void Dispose()
    {
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        shader.Dispose();
        texture.Dispose();
    }
}
