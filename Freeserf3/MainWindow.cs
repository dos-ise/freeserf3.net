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
using Freeserf3.Simulation;
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
    GameObjectRenderer? gameObjects;
    Minimap? minimap;
    MapFileReader? map;
    Freeserf3.Simulation.Map? gameMap;

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
        map.ReadBuildings();
        map.ReadSettlers();
        map.ReadStacks();
        Console.WriteLine($"Map: {map.WidthHeight}x{map.WidthHeight}, players={map.PlayerCount}, buildings={map.Buildings.Count}, settlers={map.Settlers.Count}");

        var gfx = gameData.LoadGfxFile(LandscapeMapping.LandscapeFile)
            ?? throw new InvalidOperationException("Landscape dat file (Siedler3_00.dat) not found.");
        Console.WriteLine($"Landscape images: {gfx.LandscapeCount}");

        terrain = new TerrainRenderer(State.Gl);
        terrain.Build(map, gfx);

        // Game objects: buildings (dat 13), settlers (dat 10), roads, flags.
        var buildingsGfx = gameData.LoadGfxFile(GameObjectRenderer.BuildingsFile);
        var settlersGfx = gameData.LoadGfxFile(GameObjectRenderer.SettlersFile);
        if (buildingsGfx != null && settlersGfx != null)
        {
            gameObjects = new GameObjectRenderer(State.Gl);
            gameObjects.Load(buildingsGfx, settlersGfx);
            Console.WriteLine($"Buildings: {map.Buildings.Count}, settlers: {map.Settlers.Count}");
        }
        else
        {
            Console.WriteLine("Building/settler dat files not found; game objects disabled.");
        }

        minimap = new Minimap(State.Gl, Freeserf3.Simulation.Map.FromReader(map));
        gameMap = Freeserf3.Simulation.Map.FromReader(map);

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

            if (gameObjects != null)
            {
                gameObjects.Begin();
                CollectGameObjects();
                gameObjects.Draw(viewProjection, zoom);
            }
        }

        minimap?.Draw(window.Size.X, window.Size.Y);
    }

    /// <summary>Collects the sprites of the game objects for the current frame.</summary>
    void CollectGameObjects()
    {
        if (gameMap == null || gameObjects == null)
            return;

        int size = gameMap.Size;

        // Buildings from the map file.
        foreach (var building in gameMap.Buildings)
        {
            var type = BuildingTypeFromMap(building.BuildingType);
            if (type == null)
                continue;

            int x = building.PosX;
            int y = building.PosY;
            if (!gameMap.IsInBounds(x, y))
                continue;

            int height = gameMap.GetCell(x, y).Height;
            gameObjects.AddBuilding(type.Value, new Position(x, y), height);
        }

        // Roads and flags from the cell state.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = gameMap.GetCell(x, y);
                var pos = new Position(x, y);

                if (cell.Road == Freeserf3.Simulation.RoadState.Road)
                    gameObjects.AddRoad(pos, cell.Height);
                if (cell.HasFlag)
                    gameObjects.AddFlag(pos, cell.Height);
            }
        }
    }

    /// <summary>
    /// Maps a map-file building type byte to a game building type. The map
    /// file uses the original game order (see settlers-remake
    /// EOriginalMapBuildingType).
    /// </summary>
    static BuildingType? BuildingTypeFromMap(byte buildingType)
    {
        return buildingType switch
        {
            1 => BuildingType.Stock,
            2 => BuildingType.Lumberjack,
            3 => BuildingType.Stonecutter,
            4 => BuildingType.Sawmill,
            5 => BuildingType.Forester,
            6 => BuildingType.LookoutTower,
            7 => BuildingType.Coalmine,
            8 => BuildingType.Goldmine,
            9 => BuildingType.Ironmine,
            10 => BuildingType.Goldmelt,
            11 => BuildingType.Ironmelt,
            12 => BuildingType.Toolsmith,
            13 => BuildingType.Weaponsmith,
            14 => BuildingType.Winegrower,
            15 => BuildingType.Tower,
            16 => BuildingType.BigTower,
            17 => BuildingType.Mill,
            18 => BuildingType.Castle,
            19 => BuildingType.Barrack,
            20 => BuildingType.Baker,
            21 => BuildingType.Slaughterhouse,
            22 => BuildingType.Distillery,
            23 => BuildingType.PigFarm,
            24 => BuildingType.Farm,
            25 => BuildingType.Fisher,
            26 => BuildingType.SmallLivinghouse,
            27 => BuildingType.MediumLivinghouse,
            28 => BuildingType.BigLivinghouse,
            29 => BuildingType.Sulfurmine,
            30 => BuildingType.Waterworks,
            32 => BuildingType.Dockyard,
            33 => BuildingType.Harbor,
            34 => BuildingType.MarketPlace,
            35 => BuildingType.Hospital,
            36 => BuildingType.RiceFarm,
            37 => BuildingType.Gemsmine,
            38 => BuildingType.Brewery,
            39 => BuildingType.CharcoalBurner,
            41 => BuildingType.BigTemple,
            42 => BuildingType.Temple,
            43 => BuildingType.BigTemple,
            44 => BuildingType.Temple,
            45 => BuildingType.BigTemple,
            46 => BuildingType.Temple,
            49 => BuildingType.DonkeyFarm,
            51 => BuildingType.Beekeeping,
            52 => BuildingType.MeadBrewery,
            53 => BuildingType.Laboratory,
            54 => BuildingType.Temple,
            55 => BuildingType.BigTemple,
            56 => BuildingType.Sulfurmine,
            _ => null
        };
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
        gameObjects?.Dispose();
        gameObjects = null;
        minimap?.Dispose();
        minimap = null;
    }

    public void Dispose()
    {
        input?.Dispose();
        window.Dispose();
    }
}
