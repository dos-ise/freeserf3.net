/*
 * ShaderProgram.cs - GLSL shader program helper
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Silk.NET.OpenGL;

namespace Freeserf3.Renderer;

public sealed class ShaderProgram : IDisposable
{
    readonly GL gl;
    readonly uint handle;

    public ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        this.gl = gl;
        handle = gl.CreateProgram();

        uint vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);

        gl.AttachShader(handle, vertex);
        gl.AttachShader(handle, fragment);

        // Bind attribute locations explicitly so the shaders work on all
        // GLSL versions (layout qualifiers need GLSL 330+).
        gl.BindAttribLocation(handle, 0, "aPosition");
        gl.BindAttribLocation(handle, 1, "aUv");
        gl.BindAttribLocation(handle, 2, "aColor");

        gl.LinkProgram(handle);

        gl.GetProgram(handle, ProgramPropertyARB.LinkStatus, out int status);
        if (status != (int)GLEnum.True)
        {
            string log = gl.GetProgramInfoLog(handle);
            throw new Exception($"Shader program link failed: {log}");
        }

        gl.DetachShader(handle, vertex);
        gl.DetachShader(handle, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
    }

    uint CompileShader(ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status != (int)GLEnum.True)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new Exception($"Shader compile failed ({type}): {log}");
        }

        return shader;
    }

    public void Use()
    {
        gl.UseProgram(handle);
    }

    public int GetUniformLocation(string name)
    {
        return gl.GetUniformLocation(handle, name);
    }

    public void SetUniform1(string name, int value)
    {
        gl.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform1(string name, float value)
    {
        gl.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform2(string name, float x, float y)
    {
        gl.Uniform2(GetUniformLocation(name), x, y);
    }

    public void SetUniformMatrix4(string name, in System.Numerics.Matrix4x4 matrix)
    {
        // Silk.NET's Matrix4x4 overload is unreliable across versions; pass
        // the matrix as a float array (column-major, as OpenGL expects).
        Span<float> values = stackalloc float[16];
        values[0] = matrix.M11; values[1] = matrix.M12; values[2] = matrix.M13; values[3] = matrix.M14;
        values[4] = matrix.M21; values[5] = matrix.M22; values[6] = matrix.M23; values[7] = matrix.M24;
        values[8] = matrix.M31; values[9] = matrix.M32; values[10] = matrix.M33; values[11] = matrix.M34;
        values[12] = matrix.M41; values[13] = matrix.M42; values[14] = matrix.M43; values[15] = matrix.M44;
        gl.UniformMatrix4(GetUniformLocation(name), 1, false, values);
    }

    public void Dispose()
    {
        gl.DeleteProgram(handle);
    }
}
