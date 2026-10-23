/*
 * SpriteRenderer.cs - Draws textured sprites on the map
 *
 * Sprites are positioned in map pixel space (the same space the terrain
 * uses after the grid->screen conversion) and drawn with the view
 * projection. Each sprite is a textured quad with a z-order for depth
 * sorting.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Render;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>A textured quad to be drawn.</summary>
public readonly struct Sprite
{
    public Sprite(TextureRegion region, float x, float y, float width, float height, float z = 0f)
    {
        Region = region;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Z = z;
    }

    public TextureRegion Region { get; }
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public float Z { get; }
}

/// <summary>
/// Renders a batch of textured quads. Sprites are collected per frame and
/// drawn in z-order (painter's algorithm).
/// </summary>
public sealed class SpriteRenderer : IDisposable
{
    readonly GL gl;
    readonly ShaderProgram shader;
    readonly List<Sprite> sprites = new();
    uint vao;
    uint vbo;
    int vertexCount;

    public SpriteRenderer(GL gl)
    {
        this.gl = gl;
        shader = CreateShader(gl);

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        // Vertex layout: position (2 floats), uv (2 floats).
        const uint stride = 4 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        gl.BindVertexArray(0);
    }

    /// <summary>Clears the sprite list for a new frame.</summary>
    public void Begin()
    {
        sprites.Clear();
    }

    /// <summary>Adds a sprite to the current frame.</summary>
    public void Add(Sprite sprite)
    {
        sprites.Add(sprite);
    }

    /// <summary>Adds a sprite from a texture region.</summary>
    public void Add(TextureRegion region, float x, float y, float width, float height, float z = 0f)
    {
        sprites.Add(new Sprite(region, x, y, width, height, z));
    }

    /// <summary>Number of sprites in the current frame.</summary>
    public int Count => sprites.Count;

    /// <summary>Draws all sprites with the given view transform.</summary>
    public void Draw(in Matrix4x4 viewProjection, float zoom)
    {
        if (sprites.Count == 0)
            return;

        // Sort by z-order (painter's algorithm).
        var sorted = sprites.OrderBy(s => s.Z).ToList();

        var vertices = new List<float>(sorted.Count * 4 * 4);
        foreach (var sprite in sorted)
        {
            float x0 = sprite.X;
            float y0 = sprite.Y;
            float x1 = sprite.X + sprite.Width;
            float y1 = sprite.Y + sprite.Height;

            // Two triangles (quad).
            AddVertex(vertices, x0, y0, sprite.Region.U0, sprite.Region.V0);
            AddVertex(vertices, x1, y0, sprite.Region.U1, sprite.Region.V0);
            AddVertex(vertices, x1, y1, sprite.Region.U1, sprite.Region.V1);
            AddVertex(vertices, x0, y0, sprite.Region.U0, sprite.Region.V0);
            AddVertex(vertices, x1, y1, sprite.Region.U1, sprite.Region.V1);
            AddVertex(vertices, x0, y1, sprite.Region.U0, sprite.Region.V1);
        }

        vertexCount = vertices.Count / 4;

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, vertices.ToArray(), BufferUsageARB.DynamicDraw);

        shader.Use();
        shader.SetUniformMatrix4("uViewProjection", viewProjection);
        shader.SetUniform1("uZoom", zoom);

        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertexCount);
        gl.BindVertexArray(0);
    }

    static void AddVertex(List<float> vertices, float x, float y, float u, float v)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(u);
        vertices.Add(v);
    }

    static ShaderProgram CreateShader(GL gl)
    {
        string version = State.GetShaderVersionDirective();
        string precision = State.IsOpenGLES ? "precision mediump float;\n" : "";

        string vertexSource = $@"{version}
in vec2 aPosition;
in vec2 aUv;

uniform mat4 uViewProjection;
uniform float uZoom;

out vec2 vUv;

void main()
{{
    vec4 pos = vec4(aPosition * uZoom, 0.0, 1.0);
    gl_Position = uViewProjection * pos;
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
    }
}
