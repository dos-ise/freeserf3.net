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
                if (!BitmapFont.Glyphs.TryGetValue(char.ToUpperInvariant(c), out var glyph))
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
}
