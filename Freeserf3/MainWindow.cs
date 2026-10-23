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

/// <summary>The interaction modes of the map view.</summary>
enum InteractionMode
{
    /// <summary>Click selects buildings; drag pans the camera.</summary>
    Select,

    /// <summary>Click places the selected building type.</summary>
    Build,

    /// <summary>Click places road cells.</summary>
    Road,

    /// <summary>Click places flags.</summary>
    Flag
}

public sealed class MainWindow : IDisposable
{
    readonly GameData gameData;
    readonly IWindow window;
    IInputContext? input;
    TerrainRenderer? terrain;
    GameObjectRenderer? gameObjects;
    Minimap? minimap;
    HudRenderer? hud;
    MapFileReader? map;
    Freeserf3.Simulation.Map? gameMap;
    Freeserf3.Simulation.Game? game;

    // Camera state.
    Vector2 cameraOffset;
    float zoom = 1.0f;
    bool dragging;
    Vector2 lastMouse;

    // Interaction state.
    InteractionMode mode = InteractionMode.Select;
    BuildingType buildType = BuildingType.Lumberjack;
    Position? selected;

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
        // Prefer single-player maps, which contain pre-placed buildings and
        // settlers.
        string? mapFile = Program.MapFile;
        if (mapFile == null)
        {
            mapFile = gameData.FindSinglePlayerMapFiles().FirstOrDefault();
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
            var playerColors = map.Settlers.Select(s => (int)s.Party)
                .Concat(map.Buildings.Select(b => (int)b.Party))
                .Distinct();
            gameObjects.Load(buildingsGfx, settlersGfx, playerColors);
            Console.WriteLine($"Buildings: {map.Buildings.Count}, settlers: {map.Settlers.Count}");
        }
        else
        {
            Console.WriteLine("Building/settler dat files not found; game objects disabled.");
        }

        minimap = new Minimap(State.Gl, Freeserf3.Simulation.Map.FromReader(map));
        gameMap = Freeserf3.Simulation.Map.FromReader(map);
        hud = new HudRenderer(State.Gl);

        // Start a new round: players, starting resources and population.
        game = new Freeserf3.Simulation.Game(gameMap, 42);
        game.StartGame();
        Console.WriteLine($"Round started: {game.Players.Count} players, target population {game.TargetPopulation}");

        // Center the camera on the map. The map's screen-space bounding box
        // is x: [-(size-1)*8, (size-1)*16], y: [0, (size-1)*9]; its center
        // is ((size-1)*4, (size-1)*4.5). The projection is centered on the
        // origin, so translating the map center to (0,0) puts it in the
        // middle of the screen.
        int size = map.WidthHeight;
        cameraOffset = new Vector2(-(size - 1) * 4f, -(size - 1) * 4.5f);

        // Fit the whole map into the window.
        float mapWidth = (size - 1) * 24f;
        float mapHeight = (size - 1) * 9f;
        zoom = MathF.Min(window.Size.X / mapWidth, window.Size.Y / mapHeight);

        Console.WriteLine("Controls: drag=pan, wheel=zoom, B=build, R=road, F=flag, T=next building, right-click=cancel, Esc=close");

        State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
    }

    void OnRender(double deltaTime)
    {
        State.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Advance the simulation (60 ticks per second).
        game?.TickOnce();

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
        hud?.Draw(window.Size.X, window.Size.Y, BuildHudLines());
    }

    /// <summary>Builds the HUD text lines (resources, population, menu).</summary>
    List<string> BuildHudLines()
    {
        var lines = new List<string>();
        if (game == null)
            return lines;

        var player = game.GetPlayer(0);
        if (player == null)
            return lines;

        lines.Add($"POP {player.Population}/{game.TargetPopulation}  FREE {player.FreeSettlers}");
        lines.Add($"PLANK {player.Inventory.Get(Material.Plank)}  STONE {player.Inventory.Get(Material.Stone)}");
        lines.Add($"TRUNK {player.Inventory.Get(Material.Trunk)}  BREAD {player.Inventory.Get(Material.Bread)}");
        lines.Add($"BUILD: {buildType}  MODE: {mode}");

        if (game.IsWon)
            lines.Add("YOU WIN! POPULATION TARGET REACHED");

        return lines;
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

        // Buildings placed by the player (from the game simulation).
        if (game != null)
        {
            foreach (var building in game.Buildings)
            {
                if (building.State == BuildingState.Destroyed)
                    continue;
                if (!gameMap.IsInBounds(building.Position))
                    continue;
                gameObjects.AddBuilding(building.Type, building.Position, gameMap.GetCell(building.Position).Height);
            }
        }

        // Settlers placed on the map. The animation frame follows the game
        // clock (or wall time before the game starts).
        int frame = (int)((game?.Tick ?? (ulong)Environment.TickCount) / 10) % 8;
        foreach (var settler in gameMap.Settlers)
        {
            var pos = new Position(settler.PosX, settler.PosY);
            if (!gameMap.IsInBounds(pos))
                continue;
            gameObjects.AddMovable(pos, gameMap.GetCell(pos).Height, settler.Party, frame);
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

        // Selection marker.
        if (selected is Position sel && gameMap.IsInBounds(sel))
        {
            gameObjects.AddSelectionMarker(sel, gameMap.GetCell(sel).Height);
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

        // Orthographic projection centered on the origin: the map center
        // (translated to (0,0) by the camera offset) lands in the middle of
        // the screen. Y grows downward (bottom=+h/2, top=-h/2) to match the
        // map geometry.
        var projection = Matrix4x4.CreateOrthographicOffCenter(-width / 2, width / 2, height / 2, -height / 2, -1, 1);

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

            // A click (no significant drag) performs the interaction.
            var position = new Vector2(mouse.Position.X, mouse.Position.Y);
            if (Vector2.Distance(position, lastMouse) < 4f)
            {
                if (button == MouseButton.Left)
                    HandleLeftClick(position);
                else
                    HandleRightClick();
            }
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

    /// <summary>Converts a window position to a map grid position.</summary>
    Position MouseToGrid(Vector2 mousePos)
    {
        // The projection is centered on the origin, so the window center
        // corresponds to map pixel (0,0) before the camera offset.
        float mapX = (mousePos.X - window.Size.X / 2f) / zoom - cameraOffset.X;
        float mapY = (mousePos.Y - window.Size.Y / 2f) / zoom - cameraOffset.Y;
        return MapGeometry.ScreenToGrid(mapX, mapY);
    }

    /// <summary>Handles a left click: select or place depending on the mode.</summary>
    void HandleLeftClick(Vector2 mousePos)
    {
        if (gameMap == null)
            return;

        var pos = MouseToGrid(mousePos);
        if (!gameMap.IsInBounds(pos))
            return;

        switch (mode)
        {
            case InteractionMode.Select:
                SelectAt(pos);
                break;
            case InteractionMode.Build:
                PlaceBuilding(pos);
                break;
            case InteractionMode.Road:
                PlaceRoad(pos);
                break;
            case InteractionMode.Flag:
                PlaceFlag(pos);
                break;
        }
    }

    /// <summary>Handles a right click: back to select mode.</summary>
    void HandleRightClick()
    {
        if (mode != InteractionMode.Select)
        {
            mode = InteractionMode.Select;
            Console.WriteLine("Mode: select");
        }
    }

    /// <summary>Selects the building at the given position, if any.</summary>
    void SelectAt(Position pos)
    {
        selected = null;

        if (game != null)
        {
            foreach (var building in game.Buildings)
            {
                if (building.Position == pos)
                {
                    selected = pos;
                    Console.WriteLine($"Selected {building.Type} at {pos} ({building.State})");
                    return;
                }
            }
        }

        foreach (var building in gameMap!.Buildings)
        {
            if (building.PosX == pos.X && building.PosY == pos.Y)
            {
                selected = pos;
                var type = BuildingTypeFromMap(building.BuildingType);
                Console.WriteLine($"Selected {(type?.ToString() ?? "building")} at {pos}");
                return;
            }
        }
    }

    /// <summary>Places the current building type at the given position.</summary>
    void PlaceBuilding(Position pos)
    {
        if (gameMap == null || game == null)
            return;

        // Do not place on water or on an existing building.
        var cell = gameMap.GetCell(pos);
        var landscape = LandscapeMapping.FromMapValue(cell.Landscape);
        if (landscape is LandscapeType.Water1 or LandscapeType.Water2 or LandscapeType.Water3
            or LandscapeType.Water4 or LandscapeType.Water5 or LandscapeType.Water6
            or LandscapeType.Water7 or LandscapeType.Water8)
        {
            Console.WriteLine("Cannot build on water.");
            return;
        }

        var building = game.PlaceBuilding(buildType, pos, 0);
        Console.WriteLine($"Placed {building.Type} at {pos} ({building.State})");
    }

    /// <summary>Places a road cell at the given position.</summary>
    void PlaceRoad(Position pos)
    {
        if (gameMap == null)
            return;

        var modifier = new MapModifier(gameMap);
        modifier.PlaceRoad(pos);
        Console.WriteLine($"Road at {pos}");
    }

    /// <summary>Places a flag at the given position.</summary>
    void PlaceFlag(Position pos)
    {
        if (gameMap == null)
            return;

        var modifier = new MapModifier(gameMap);
        modifier.SetFlag(pos, true);
        Console.WriteLine($"Flag at {pos}");
    }

    void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        switch (key)
        {
            case Key.Escape:
                if (mode != InteractionMode.Select)
                {
                    mode = InteractionMode.Select;
                    Console.WriteLine("Mode: select");
                }
                else
                {
                    window.Close();
                }
                break;
            case Key.B:
                mode = InteractionMode.Build;
                Console.WriteLine($"Mode: build ({buildType})");
                break;
            case Key.R:
                mode = InteractionMode.Road;
                Console.WriteLine("Mode: road");
                break;
            case Key.F:
                mode = InteractionMode.Flag;
                Console.WriteLine("Mode: flag");
                break;
            case Key.T:
                if (mode == InteractionMode.Build)
                {
                    var types = Enum.GetValues<BuildingType>();
                    int index = (Array.IndexOf(types, buildType) + 1) % types.Length;
                    buildType = types[index];
                    Console.WriteLine($"Build type: {buildType}");
                }
                break;
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
