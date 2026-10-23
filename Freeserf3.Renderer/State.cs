/*
 * State.cs - OpenGL state
 *
 * Adapted from freeserf.net (GPL v3) State.cs.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using System.Text.RegularExpressions;

namespace Freeserf3.Renderer;

public static class State
{
    public static int OpenGLVersionMajor { get; private set; }
    public static int OpenGLVersionMinor { get; private set; }
    public static int GLSLVersionMajor { get; private set; }
    public static int GLSLVersionMinor { get; private set; }
    public static bool IsOpenGLES { get; private set; }

    const int MinOpenGLVersionMajor = 3;
    const int MinGLSLVersion = 130;
    const int MinGLSLESVersion = 300;

    public static GL Gl { get; private set; } = null!;

    public static void Init(IGLContextSource contextSource)
    {
        Gl = GL.GetApi(contextSource);

        string openGLVersion = Gl.GetStringS(StringName.Version).TrimStart();
        var versionRegex = new Regex(@"([0-9]+)\.([0-9]+)", RegexOptions.Compiled);
        var match = versionRegex.Match(openGLVersion);

        if (!match.Success || match.Groups.Count < 3)
        {
            throw new Exception("OpenGL is not supported or the version could not be determined.");
        }

        OpenGLVersionMajor = int.Parse(match.Groups[1].Value);
        OpenGLVersionMinor = int.Parse(match.Groups[2].Value);
        IsOpenGLES = openGLVersion.StartsWith("OpenGL ES", StringComparison.OrdinalIgnoreCase);

        if (OpenGLVersionMajor >= 2)
        {
            string glslVersion = Gl.GetStringS(StringName.ShadingLanguageVersion);
            match = versionRegex.Match(glslVersion);
            if (match.Success && match.Groups.Count >= 3)
            {
                GLSLVersionMajor = int.Parse(match.Groups[1].Value);
                GLSLVersionMinor = int.Parse(match.Groups[2].Value);
            }
        }

        CheckRequirements(openGLVersion);
    }

    static void CheckRequirements(string openGLVersion)
    {
        int glslVersion = GLSLVersionMajor * 100 + GLSLVersionMinor;
        int minGLSLVersion = IsOpenGLES ? MinGLSLESVersion : MinGLSLVersion;

        if (OpenGLVersionMajor < MinOpenGLVersionMajor || glslVersion < minGLSLVersion)
        {
            string required = IsOpenGLES ? "OpenGL ES 3.0 (GLSL ES 3.00)" : "OpenGL 3.0 (GLSL 1.30)";
            string available = GLSLVersionMajor == 0
                ? $"'{openGLVersion}' without shaders"
                : $"'{openGLVersion}' with GLSL {GLSLVersionMajor}.{GLSLVersionMinor:00}";

            throw new NotSupportedException(
                $"{required} or newer is required, but the graphics driver only provides {available}. Please update the graphics driver.");
        }
    }

    /// <summary>The #version directive for the shaders.</summary>
    public static string GetShaderVersionDirective() =>
        $"#version {GLSLVersionMajor}{GLSLVersionMinor:00}{(IsOpenGLES ? " es" : "")}";
}
