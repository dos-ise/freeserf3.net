/*
 * TerrainRenderer.cs - Renders the Settlers 3 terrain
 *
 * Geometry approach ported from settlers-remake (MIT) Background.java:
 * each map cell is split into two triangles (up/down); the texture for each
 * triangle is the base landscape image or a border transition texture.
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

/// <summary>Renders the terrain of a Settlers 3 map.</summary>
public sealed class TerrainRenderer : IDisposable
{
    // Hex grid geometry (from settlers-remake DrawConstants).
    const float DistanceX = 16f;
    const float DistanceY = 9f;
    const float HeightScale = 2f;

    // Texture coordinates (normalized within a 32x32 tile) for the two
    // triangle orientations. From settlers-remake ETextureOrientation.java,
    // converted from pixel units (TEXTUREUNIT=16, TEXTURE_GRID=32) to [0,1].
    static readonly float[] ContinuousUp = { 0.25f, 0f, 0f, 0.5f, 0.5f, 0.5f };
    static readonly float[] ContinuousDown = { 0.25f, 0f, 0.5f, 0.5f, 0.75f, 0f };
    static readonly float[] Bottom = { 0.5f, 0.5f, 0.25f, 1f, 0.75f, 1f };
    static readonly float[] Top = { 0.25f, 0f, 0.5f, 0.5f, 0.75f, 0f };
    static readonly float[] TopRight = { 0.75f, 0f, 0.5f, 0.5f, 1f, 0.5f };
    static readonly float[] BottomRight = { 0.5f, 0.5f, 0.75f, 1f, 1f, 0.5f };
    static readonly float[] TopLeft = { 0.25f, 0f, 0f, 0.5f, 0.5f, 0.5f };
    static readonly float[] BottomLeft = { 0f, 0.5f, 0.25f, 1f, 0.5f, 0.5f };

    readonly GL gl;
    readonly ShaderProgram shader;
    readonly TextureAtlas atlas;
    readonly Dictionary<int, TextureRegion> textureRegions = new();
    readonly uint vao;
    readonly uint vbo;
    readonly uint ebo;
    int vertexCount;
    int indexCount;
    int size;
    float[] vertices = Array.Empty<float>();
    readonly List<float> baseColors = new();
    readonly List<int> vertexCells = new();
    float[] cellFog = Array.Empty<float>();

    public TerrainRenderer(GL gl)
    {
        this.gl = gl;
        atlas = new TextureAtlas(gl);
        shader = CreateShader(gl);

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

        // Vertex layout: position (3 floats), uv (2 floats), color (1 float).
        const uint stride = 6 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 5 * sizeof(float));

        gl.BindVertexArray(0);
    }

    /// <summary>Builds the terrain geometry from a map and its landscape images.</summary>
    public void Build(MapFileReader map, DatFileReader gfx)
    {
        var area = map.Area ?? throw new InvalidOperationException("Map area not loaded.");
        LoadLandscapeImages(gfx);
        BuildGeometry(map.WidthHeight,
            (x, y) => area[y * map.WidthHeight + x].Landscape,
            (x, y) => area[y * map.WidthHeight + x].Height);
    }

    /// <summary>
    /// Rebuilds the terrain geometry from the game map (used after the
    /// ground under a building was changed to sand). The landscape images
    /// stay in the atlas.
    /// </summary>
    public void Rebuild(Map map)
    {
        BuildGeometry(map.Size,
            (x, y) => map.GetCell(x, y).Landscape,
            (x, y) => map.GetCell(x, y).Height);
    }

    void LoadLandscapeImages(DatFileReader gfx)
    {
        for (int i = 0; i < gfx.LandscapeCount; i++)
        {
            var image = gfx.GetLandscape(i);
            if (image == null)
                continue;
            textureRegions[i] = atlas.AddImage(image.Rgba, image.Width, image.Height);
        }
    }

    void BuildGeometry(int size, Func<int, int, byte> getLandscape, Func<int, int, byte> getHeight)
    {
        this.size = size;

        // Build the vertex data: 2 triangles per cell, 3 vertices each.
        var vertices = new List<float>(size * size * 2 * 3 * 6);
        var indices = new List<uint>(size * size * 2 * 3);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                AddCell(vertices, indices, size, getLandscape, getHeight, x, y);
            }
        }

        vertexCount = vertices.Count / 6;
        indexCount = indices.Count;
        this.vertices = vertices.ToArray();
        cellFog = new float[size * size];

        Console.WriteLine($"Terrain: {vertexCount} vertices, {indexCount} indices, atlas {atlas.Texture.Width}x{atlas.Texture.Height}");

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, vertices.ToArray(), BufferUsageARB.StaticDraw);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, indices.ToArray(), BufferUsageARB.StaticDraw);
        gl.BindVertexArray(0);
    }

    void AddCell(List<float> vertices, List<uint> indices, int size,
        Func<int, int, byte> getLandscape, Func<int, int, byte> getHeight, int x, int y)
    {
        LandscapeType? GetType(int gx, int gy)
        {
            if (gx < 0 || gx >= size || gy < 0 || gy >= size)
                return null;
            return LandscapeMapping.FromMapValue(getLandscape(gx, gy));
        }

        byte GetHeightAt(int gx, int gy)
        {
            if (gx < 0 || gx >= size || gy < 0 || gy >= size)
                return 0;
            return getHeight(gx, gy);
        }

        // Up triangle: (x, y), (x, y+1), (x+1, y+1)
        // (vertex order matches settlers-remake Background.java: the top
        // vertex (x, y) gets the first texture coordinate)
        var leftUp = GetType(x, y + 1) ?? LandscapeType.Grass;
        var centerUp = GetType(x, y) ?? LandscapeType.Grass;
        var rightUp = GetType(x + 1, y + 1) ?? LandscapeType.Grass;
        int texUp = LandscapeMapping.GetTextureIndex(leftUp, centerUp, rightUp, (x * 37 + y * 17) & 1);
        AddTriangle(vertices, indices,
            (x, y, GetHeightAt(x, y)),
            (x, y + 1, GetHeightAt(x, y + 1)),
            (x + 1, y + 1, GetHeightAt(x + 1, y + 1)),
            texUp, true, leftUp == rightUp, leftUp == centerUp, y * size + x);

        // Down triangle: (x, y), (x+1, y+1), (x+1, y)
        var leftDown = GetType(x, y) ?? LandscapeType.Grass;
        var centerDown = GetType(x + 1, y + 1) ?? LandscapeType.Grass;
        var rightDown = GetType(x + 1, y) ?? LandscapeType.Grass;
        int texDown = LandscapeMapping.GetTextureIndex(leftDown, centerDown, rightDown, x & 1);
        AddTriangle(vertices, indices,
            (x, y, GetHeightAt(x, y)),
            (x + 1, y + 1, GetHeightAt(x + 1, y + 1)),
            (x + 1, y, GetHeightAt(x + 1, y)),
            texDown, false, leftDown == rightDown, leftDown == centerDown, y * size + x);
    }

    void AddTriangle(List<float> vertices, List<uint> indices,
        (int X, int Y, int Height) p1, (int X, int Y, int Height) p2, (int X, int Y, int Height) p3,
        int textureIndex, bool up, bool leftEqualsRight, bool leftEqualsCenter, int cellIndex)
    {
        if (!textureRegions.TryGetValue(textureIndex, out var region))
            return;

        float[] texCoords;
        if (leftEqualsRight && leftEqualsCenter)
        {
            texCoords = up ? ContinuousUp : ContinuousDown;
        }
        else if (leftEqualsRight)
        {
            texCoords = up ? Bottom : Top;
        }
        else if (leftEqualsCenter)
        {
            texCoords = up ? TopLeft : BottomLeft;
        }
        else
        {
            texCoords = up ? TopRight : BottomRight;
        }

        uint baseIndex = (uint)(vertices.Count / 6);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);

        AddVertex(vertices, p1, texCoords[0], texCoords[1], region, cellIndex);
        AddVertex(vertices, p2, texCoords[2], texCoords[3], region, cellIndex);
        AddVertex(vertices, p3, texCoords[4], texCoords[5], region, cellIndex);
    }

    void AddVertex(List<float> vertices, (int X, int Y, int Height) p, float u, float v, TextureRegion region, int cellIndex)
    {
        float color = 0.875f;
        vertices.Add(p.X);
        vertices.Add(p.Y);
        vertices.Add(p.Height);
        vertices.Add(region.U0 + u * (region.U1 - region.U0));
        vertices.Add(region.V0 + v * (region.V1 - region.V0));
        vertices.Add(color);
        baseColors.Add(color);
        vertexCells.Add(cellIndex);
    }

    /// <summary>
    /// Updates the fog of war shading of the terrain. The color of each
    /// vertex is multiplied by the visibility of its cell (like
    /// settlers-remake Background.java): unexplored cells are black,
    /// explored cells are half bright and visible cells are full bright.
    /// </summary>
    public void UpdateFog(FogOfWar fog)
    {
        for (int i = 0; i < cellFog.Length; i++)
        {
            cellFog[i] = fog.GetSight(i % size, i / size) switch
            {
                FogState.Unexplored => 0f,
                FogState.Explored => 0.5f,
                _ => 1f
            };
        }

        for (int i = 0; i < vertexCount; i++)
            vertices[i * 6 + 5] = baseColors[i] * cellFog[vertexCells[i]];

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, vertices);
        gl.BindVertexArray(0);
    }

    /// <summary>Draws the terrain with the given view transform.</summary>
    public void Draw(in Matrix4x4 viewProjection, float zoom)
    {
        shader.Use();
        shader.SetUniformMatrix4("uViewProjection", viewProjection);
        shader.SetUniform1("uZoom", zoom);
        shader.SetUniform1("uDistanceX", DistanceX);
        shader.SetUniform1("uDistanceY", DistanceY);
        shader.SetUniform1("uHeightScale", HeightScale);

        atlas.Texture.Bind(0);
        shader.SetUniform1("uTexture", 0);

        gl.BindVertexArray(vao);
        unsafe
        {
            gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
        gl.BindVertexArray(0);
    }

    static ShaderProgram CreateShader(GL gl)
    {
        string version = State.GetShaderVersionDirective();
        // GLES fragment shaders need an explicit default float precision.
        string precision = State.IsOpenGLES ? "precision mediump float;\n" : "";

        // The #version directive must be the very first line (GLES is strict).
        string vertexSource = $@"{version}
in vec3 aPosition;
in vec2 aUv;
in float aColor;

uniform mat4 uViewProjection;
uniform float uZoom;
uniform float uDistanceX;
uniform float uDistanceY;
uniform float uHeightScale;

out vec2 vUv;
out float vColor;

void main()
{{
    float screenX = aPosition.x * uDistanceX - aPosition.y * uDistanceX / 2.0;
    float screenY = aPosition.y * uDistanceY - aPosition.z * uHeightScale;
    vec4 pos = vec4(screenX * uZoom, screenY * uZoom, 0.0, 1.0);
    gl_Position = uViewProjection * pos;
    vUv = aUv;
    vColor = aColor;
}}
";

        string fragmentSource = $@"{version}
{precision}in vec2 vUv;
in float vColor;

uniform sampler2D uTexture;

out vec4 fragColor;

void main()
{{
    vec4 tex = texture(uTexture, vUv);
    fragColor = vec4(tex.rgb * vColor, tex.a);
}}
";

        return new ShaderProgram(gl, vertexSource, fragmentSource);
    }

    public void Dispose()
    {
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        shader.Dispose();
        atlas.Dispose();
    }
}
