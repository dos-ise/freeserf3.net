/*
 * MainWindow.cs - freeserf3.net desktop window and render loop
 *
 * Thin host: creates the window, forwards input events to the shared
 * GameView and renders one frame per update. All game logic lives in
 * Freeserf3.Renderer.GameView.
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
    GameView? gameView;
    WindowsSoundPlayer? soundPlayer;

    // Camera drag state.
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

        gameView = new GameView(gameData, Console.WriteLine);
        gameView.ExitRequested += () => window.Close();
        soundPlayer = new WindowsSoundPlayer();
        gameView.SetSoundPlayer(soundPlayer);
        if (!gameView.Load(Program.MapFile, window.Size.X, window.Size.Y))
        {
            throw new InvalidOperationException("Failed to load the game.");
        }

        Console.WriteLine("Controls: drag=pan, wheel=zoom, B=build, R=road, F=flag, T=next building, right-click=cancel, Esc=menu/close");
    }

    void OnRender(double deltaTime)
    {
        gameView?.Render(window.Size.X, window.Size.Y);
    }

    void OnResize(Vector2D<int> size)
    {
        State.Gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    }

    void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left || button == MouseButton.Right)
        {
            // Clicks on the control panel are handled by the panel, not the map.
            if (button == MouseButton.Left && gameView != null
                && gameView.IsInPanel(new Vector2(mouse.Position.X, mouse.Position.Y), window.Size.X, window.Size.Y))
                return;

            dragging = true;
            lastMouse = new Vector2(mouse.Position.X, mouse.Position.Y);
        }
    }

    void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left || button == MouseButton.Right)
        {
            dragging = false;

            // A click (no significant drag) performs the interaction.
            var position = new Vector2(mouse.Position.X, mouse.Position.Y);
            if (Vector2.Distance(position, lastMouse) < 4f && gameView != null)
            {
                if (button == MouseButton.Left)
                {
                    if (gameView.HandlePanelClick(position))
                        return;
                    gameView.HandleClick(position, window.Size.X, window.Size.Y);
                }
                else
                {
                    gameView.HandleRightClick();
                }
            }
        }
    }

    void OnMouseMove(IMouse mouse, Vector2 position)
    {
        gameView?.SetMousePosition(position);

        if (dragging)
        {
            var delta = new Vector2(position.X, position.Y) - lastMouse;
            gameView?.Pan(delta);
            lastMouse = new Vector2(position.X, position.Y);
        }
    }

    void OnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        float factor = scrollWheel.Y > 0 ? 1.1f : 1.0f / 1.1f;
        gameView?.Zoom(factor);
    }

    void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (gameView == null)
            return;

        switch (key)
        {
            case Key.Escape:
                if (gameView.IsMenuOpen)
                    gameView.CloseMenu();
                else if (gameView.Mode != InteractionMode.Select)
                    gameView.CancelMode();
                else
                    window.Close();
                break;
            case Key.B:
                gameView.SetMode(InteractionMode.Build);
                break;
            case Key.R:
                gameView.SetMode(InteractionMode.Road);
                break;
            case Key.F:
                gameView.SetMode(InteractionMode.Flag);
                break;
            case Key.T:
                if (gameView.Mode == InteractionMode.Build)
                    gameView.NextBuilding();
                break;
            case Key.C:
                gameView.CycleToolsmithOutput();
                break;
            case Key.X:
                gameView.DemolishSelectedBuilding();
                break;
        }
    }

    void OnClosing()
    {
        gameView?.Dispose();
        gameView = null;
        soundPlayer?.Dispose();
        soundPlayer = null;
    }

    public void Dispose()
    {
        input?.Dispose();
        window.Dispose();
    }
}
