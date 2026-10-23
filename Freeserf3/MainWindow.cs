/*
 * MainWindow.cs - freeserf3.net desktop window and render loop
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Renderer;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Freeserf3;

public sealed class MainWindow : IDisposable
{
    readonly GameData gameData;
    readonly IWindow window;
    IInputContext? input;
    TerrainRenderer? terrain;
    MapFileReader? map;

    // Camera state.
    Vector2 cameraOffset;
    float zoom = 1.0f;
    bool dragging;
    Vector2 lastMouse;

    public MainWindow(GameData gameData)
    {
        this.gameData = gameData;

        var options = WindowOptions.Default with
        {
            Title = "freeserf3.net - Settlers 3",
            Size = new Vector2D<int>(1280, 800),
            UpdatesPerSecond = 60.0,
            FramesPerSecond = 60.0,
            API = GraphicsAPI.Default
        };

        window = Window.Create(options);
        window.Load += OnLoad;
        window.Render += OnRender;
        window.Resize += OnResize;
        window.Closing += OnClosing;
    }

    public void Run()
    {
        window.Run();
    }

    void OnLoad()
    {
        State.Init(window);
        Console.WriteLine($"OpenGL: {State.OpenGLVersionMajor}.{State.OpenGLVersionMinor} (ES={State.IsOpenGLES}), GLSL: {State.GLSLVersionMajor}.{State.GLSLVersionMinor:00}");

        input = window.CreateInput();
        input.Mice[0].MouseDown += OnMouseDown;
        input.Mice[0].MouseUp += OnMouseUp;
        input.Mice[0].MouseMove += OnMouseMove;
        input.Mice[0].Scroll += OnScroll;
        input.Keyboards[0].KeyDown += OnKeyDown;

        // Load the map (first found or the one given on the command line).
        string? mapFile = Program.MapFile;
        if (mapFile == null)
        {
            mapFile = gameData.FindMapFiles().FirstOrDefault();
        }

        if (mapFile == null)
        {
            throw new InvalidOperationException("No map file found in the game data.");
        }

        Console.WriteLine($"Loading map: {mapFile}");
        map = gameData.LoadMap(mapFile);
        map.LoadMapResources();
        map.ReadBasicMapInformation();
        map.ReadMapData();
        Console.WriteLine($"Map: {map.WidthHeight}x{map.WidthHeight}, players={map.PlayerCount}");

        var gfx = gameData.LoadGfxFile(LandscapeMapping.LandscapeFile)
            ?? throw new InvalidOperationException("Landscape dat file (Siedler3_00.dat) not found.");
        Console.WriteLine($"Landscape images: {gfx.LandscapeCount}");

        terrain = new TerrainRenderer(State.Gl);
        terrain.Build(map, gfx);

        // Center the camera on the map. The map's screen-space bounding box
        // is x: [-(size-1)*8, (size-1)*16], y: [0, (size-1)*16].
        int size = map.WidthHeight;
        cameraOffset = new Vector2(-(size - 1) * 4f, -(size - 1) * 8f);

        State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
    }

    void OnRender(double deltaTime)
    {
        State.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (terrain != null)
        {
            var viewProjection = CreateViewProjection();
            terrain.Draw(viewProjection, zoom);
        }
    }

    Matrix4x4 CreateViewProjection()
    {
        float width = window.Size.X;
        float height = window.Size.Y;

        // Orthographic projection in screen pixels.
        var projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);

        // Camera translation (in map pixels, scaled by zoom).
        var view = Matrix4x4.CreateTranslation(cameraOffset.X * zoom, cameraOffset.Y * zoom, 0);

        return view * projection;
    }

    void OnResize(Vector2D<int> size)
    {
        State.Gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    }

    void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left || button == MouseButton.Right)
        {
            dragging = true;
            lastMouse = new Vector2(mouse.Position.X, mouse.Position.Y);
        }
    }

    void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left || button == MouseButton.Right)
        {
            dragging = false;
        }
    }

    void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (dragging)
        {
            var delta = new Vector2(position.X, position.Y) - lastMouse;
            cameraOffset += delta / zoom;
            lastMouse = new Vector2(position.X, position.Y);
        }
    }

    void OnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        float factor = scrollWheel.Y > 0 ? 1.1f : 1.0f / 1.1f;
        zoom = Math.Clamp(zoom * factor, 0.1f, 10.0f);
    }

    void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (key == Key.Escape)
        {
            window.Close();
        }
    }

    void OnClosing()
    {
        terrain?.Dispose();
        terrain = null;
    }

    public void Dispose()
    {
        input?.Dispose();
        window.Dispose();
    }
}
