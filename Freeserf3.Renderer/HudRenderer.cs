/*
 * HudRenderer.cs - Simple on-screen HUD (resources, population, menu)
 *
 * Draws a semi-transparent panel with text lines in the top-left corner.
 * Text is rendered with a built-in 5x7 bitmap font as colored quads, so it
 * works on both desktop OpenGL and Android GLES without any font files.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>Renders a small HUD panel with text in a screen corner.</summary>
public sealed class HudRenderer : IDisposable
{
    readonly GL gl;
    readonly ShaderProgram shader;
    readonly uint vao;
    readonly uint vbo;
    readonly List<float> vertices = new();

    // 5x7 bitmap font: each glyph is 7 bytes, the low 5 bits of each byte
    // are the pixels (bit 4 = leftmost).
    static readonly Dictionary<char, byte[]> Font = CreateFont();

    public HudRenderer(GL gl)
    {
        this.gl = gl;
        shader = CreateShader(gl);

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        const uint stride = 6 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws a HUD panel with the given lines in the top-left corner.
    /// </summary>
    public void Draw(int screenWidth, int screenHeight, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return;

        const float margin = 8f;
        const float lineHeight = 24f;
        const float charWidth = 12f;
        const float charHeight = 18f;
        const float panelWidth = 400f;
        float panelHeight = lines.Count * lineHeight + 12f;

        vertices.Clear();

        // Panel background (semi-transparent dark).
        AddQuad(margin, margin, panelWidth, panelHeight, new Vector4(0f, 0f, 0f, 0.55f));

        // Text lines.
        for (int i = 0; i < lines.Count; i++)
        {
            float y = margin + 8f + i * lineHeight;
            float x = margin + 8f;
            foreach (char c in lines[i])
            {
                if (c == ' ')
                {
                    x += charWidth;
                    continue;
                }
                if (!Font.TryGetValue(char.ToUpperInvariant(c), out var glyph))
                    continue;

                for (int row = 0; row < 7; row++)
                {
                    byte bits = glyph[row];
                    for (int col = 0; col < 5; col++)
                    {
                        if ((bits & (1 << (4 - col))) == 0)
                            continue;
                        AddQuad(x + col * (charWidth / 5f), y + row * (charHeight / 7f),
                            charWidth / 5f, charHeight / 7f, Vector4.One);
                    }
                }
                x += charWidth;
            }
        }

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, vertices.ToArray(), BufferUsageARB.DynamicDraw);

        shader.Use();
        shader.SetUniformMatrix4("uProjection", Matrix4x4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1, 1));

        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(vertices.Count / 6));
        gl.BindVertexArray(0);
    }

    void AddQuad(float x, float y, float w, float h, Vector4 color)
    {
        vertices.Add(x); vertices.Add(y); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        vertices.Add(x + w); vertices.Add(y); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        vertices.Add(x + w); vertices.Add(y + h); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        vertices.Add(x); vertices.Add(y); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        vertices.Add(x + w); vertices.Add(y + h); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        vertices.Add(x); vertices.Add(y + h); vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
    }

    static ShaderProgram CreateShader(GL gl)
    {
        string version = State.GetShaderVersionDirective();
        string precision = State.IsOpenGLES ? "precision mediump float;\n" : "";

        string vertexSource = $@"{version}
in vec2 aPosition;
in vec4 aColor;

uniform mat4 uProjection;

out vec4 vColor;

void main()
{{
    gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
    vColor = aColor;
}}
";

        string fragmentSource = $@"{version}
{precision}in vec4 vColor;

out vec4 fragColor;

void main()
{{
    fragColor = vColor;
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

    static Dictionary<char, byte[]> CreateFont()
    {
        var font = new Dictionary<char, byte[]>
        {
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            [':'] = new byte[] { 0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00 },
            ['/'] = new byte[] { 0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10 },
            ['%'] = new byte[] { 0x13, 0x13, 0x02, 0x04, 0x08, 0x09, 0x09 },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            ['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
            ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            ['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04 },
            ['?'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04 }
        };
        return font;
    }
}
