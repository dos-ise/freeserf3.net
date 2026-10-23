/*
 * MainActivity.cs - freeserf3.net Android host
 *
 * Uses Silk.NET SDL with OpenGL ES 3.0. The game data is loaded from the
 * app's external files directory (see GameDataPath).
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Freeserf3.Data;
using Freeserf3.Renderer;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.Android;
using System.Numerics;

namespace Freeserf3.Android;

[Activity(Label = "freeserf3.net", MainLauncher = true, ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, Exported = true)]
public class MainActivity : SilkActivity
{
    const string Tag = "Freeserf3";

    static IView? view;
    static TerrainRenderer? terrain;
    static GL? gl;
    static Vector2 cameraOffset;
    static float zoom = 1.0f;
    static bool dragging;
    static Vector2 lastTouch;
    static float lastPinchDistance;
    static MainActivity? instance;

    public string GameDataPath => Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "gamedata");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        instance = this;
        Log.Debug(Tag, "OnCreate");
    }

    protected override void OnRun()
    {
        IView? localView = null;

        try
        {
            Log.Debug(Tag, "OnRun: start");
            SdlWindowing.RegisterPlatform();
            SdlInput.RegisterPlatform();
            SdlWindowing.Use();

            var options = new WindowOptions(
                true,
                new Vector2D<int>(0, 0),
                new Vector2D<int>(1280, 960),
                50.0,
                50.0,
                new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0)),
                "freeserf3.net",
                WindowState.Normal,
                WindowBorder.Fixed,
                false,
                true,
                new VideoMode(60),
                24);

            localView = Silk.NET.Windowing.Window.GetView(new ViewOptions(options));
            view = localView;
            view.Load += Window_Load;
            view.Render += Window_Render;
            view.Resize += Window_Resize;
            view.Closing += Window_Closing;

            view.Initialize();
            view.Run(() =>
            {
                try
                {
                    view.DoEvents();
                    if (!view.IsClosing)
                        view.DoUpdate();
                    if (!view.IsClosing)
                        view.DoRender();
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, "Run loop: " + ex);
                    throw;
                }
            });
            localView.Reset();
        }
        catch (Exception ex)
        {
            Log.Error(Tag, "Run: " + ex);
        }
        finally
        {
            try { localView?.Dispose(); } catch (Exception ex) { Log.Error(Tag, "View disposal: " + ex); }
        }
    }

    static void Window_Load()
    {
        try
        {
            Log.Debug(Tag, "Window_Load: start");
            view!.MakeCurrent();
            State.Init(view);
            gl = State.Gl;
            Log.Debug(Tag, $"OpenGL: {State.OpenGLVersionMajor}.{State.OpenGLVersionMinor} (ES={State.IsOpenGLES})");

            var activity = instance ?? throw new InvalidOperationException("Activity not initialized");
            string dataPath = activity.GameDataPath;
            Log.Debug(Tag, $"Game data path: {dataPath}");

            var gameData = new GameData(dataPath);
            if (!gameData.IsValid())
            {
                Log.Error(Tag, $"Game data not found at {dataPath}. Copy the Settlers 3 data there.");
                return;
            }

            string? mapFile = gameData.FindMapFiles().FirstOrDefault();
            if (mapFile == null)
            {
                Log.Error(Tag, "No map file found.");
                return;
            }

            Log.Debug(Tag, $"Loading map: {mapFile}");
            var map = gameData.LoadMap(mapFile);
            map.LoadMapResources();
            map.ReadBasicMapInformation();
            map.ReadMapData();
            Log.Debug(Tag, $"Map: {map.WidthHeight}x{map.WidthHeight}");

            var gfx = gameData.LoadGfxFile(LandscapeMapping.LandscapeFile);
            if (gfx == null)
            {
                Log.Error(Tag, "Landscape dat file not found.");
                return;
            }

            terrain = new TerrainRenderer(State.Gl);
            terrain.Build(map, gfx);

            int size = map.WidthHeight;
            cameraOffset = new Vector2(-(size - 1) * 4f, -(size - 1) * 8f);

            State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            Log.Debug(Tag, $"Framebuffer size: {view.FramebufferSize.X}x{view.FramebufferSize.Y}");
            Log.Debug(Tag, "Window_Load: done");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, "Window_Load: " + ex);
        }
    }

    static void Window_Render(double deltaTime)
    {
        if (gl == null || terrain == null)
            return;

        int width = view!.FramebufferSize.X;
        int height = view.FramebufferSize.Y;
        if (width <= 0 || height <= 0)
            return;

        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var viewMatrix = Matrix4x4.CreateTranslation(cameraOffset.X * zoom, cameraOffset.Y * zoom, 0);
        terrain.Draw(viewMatrix * projection, zoom);

        var error = gl.GetError();
        if (error != GLEnum.NoError)
            Log.Error(Tag, $"GL error: {error}");
    }

    static void Window_Resize(Vector2D<int> size)
    {
        gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    }

    static void Window_Closing()
    {
        terrain?.Dispose();
        terrain = null;
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e == null || terrain == null)
            return base.DispatchTouchEvent(e);

        int pointerCount = e.PointerCount;
        MotionEventActions action = e.ActionMasked;

        if (pointerCount == 1)
        {
            float x = e.GetX(0);
            float y = e.GetY(0);

            switch (action)
            {
                case MotionEventActions.Down:
                    dragging = true;
                    lastTouch = new Vector2(x, y);
                    break;
                case MotionEventActions.Move:
                    if (dragging)
                    {
                        var delta = new Vector2(x, y) - lastTouch;
                        cameraOffset += delta / zoom;
                        lastTouch = new Vector2(x, y);
                    }
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    dragging = false;
                    break;
            }
        }
        else if (pointerCount >= 2)
        {
            // Pinch to zoom.
            float dx = e.GetX(0) - e.GetX(1);
            float dy = e.GetY(0) - e.GetY(1);
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (action == MotionEventActions.PointerDown)
            {
                lastPinchDistance = distance;
            }
            else if (action == MotionEventActions.Move && lastPinchDistance > 0)
            {
                float factor = distance / lastPinchDistance;
                zoom = Math.Clamp(zoom * factor, 0.1f, 10.0f);
                lastPinchDistance = distance;
            }
        }

        return base.DispatchTouchEvent(e);
    }
}
