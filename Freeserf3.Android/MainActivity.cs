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
using Freeserf3.Simulation;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.Android;
using System.Numerics;

namespace Freeserf3.Android;

[Activity(Label = "freeserf3.net", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, Exported = true)]
public class MainActivity : SilkActivity
{
    const string Tag = "Freeserf3";

    static IView? view;
    static TerrainRenderer? terrain;
    static GameObjectRenderer? gameObjects;
    static Minimap? minimap;
    static HudRenderer? hud;
    static Map? gameMap;
    static Game? game;
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

            string? mapFile = gameData.FindSinglePlayerMapFiles().FirstOrDefault();
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
            map.ReadBuildings();
            map.ReadSettlers();
            map.ReadStacks();
            Log.Debug(Tag, $"Map: {map.WidthHeight}x{map.WidthHeight}, buildings={map.Buildings.Count}, settlers={map.Settlers.Count}");

            var gfx = gameData.LoadGfxFile(LandscapeMapping.LandscapeFile);
            if (gfx == null)
            {
                Log.Error(Tag, "Landscape dat file not found.");
                return;
            }

            terrain = new TerrainRenderer(State.Gl);
            terrain.Build(map, gfx);

            // Game objects: buildings (dat 13), settlers (dat 10), roads, flags.
            var buildingsGfx = gameData.LoadGfxFile(GameObjectRenderer.BuildingsFile);
            var settlersGfx = gameData.LoadGfxFile(GameObjectRenderer.SettlersFile);
            var objectsGfx = gameData.LoadGfxFile(GameObjectRenderer.ObjectsFile);
            if (buildingsGfx != null && settlersGfx != null)
            {
                gameObjects = new GameObjectRenderer(State.Gl);
                var playerColors = map.Settlers.Select(s => (int)s.Party)
                    .Concat(map.Buildings.Select(b => (int)b.Party))
                    .Distinct();
                gameObjects.Load(buildingsGfx, settlersGfx, playerColors);
                if (objectsGfx != null)
                    gameObjects.LoadObjects(objectsGfx, buildingsGfx, playerColors);
            }
            else
            {
                Log.Warn(Tag, "Building/settler dat files not found; game objects disabled.");
            }

            gameMap = Map.FromReader(map);
            minimap = new Minimap(State.Gl, gameMap);
            hud = new HudRenderer(State.Gl);

            // Count map objects for debugging.
            int treeCells = 0;
            int borderCells = 0;
            for (int y = 0; y < gameMap.Size; y++)
            {
                for (int x = 0; x < gameMap.Size; x++)
                {
                    var cell = gameMap.GetCell(x, y);
                    if (GameObjectRenderer.IsTreeObject(cell.MapObject))
                        treeCells++;
                    if (cell.Player != 0)
                    {
                        bool isBorder = false;
                        foreach (var direction in MapGeometry.AllDirections())
                        {
                            var neighbor = new Position(x, y).GetNeighbor(direction);
                            if (!gameMap.IsInBounds(neighbor))
                                continue;
                            if (gameMap.GetCell(neighbor).Player != cell.Player)
                            {
                                isBorder = true;
                                break;
                            }
                        }
                        if (isBorder)
                            borderCells++;
                    }
                }
            }
            Log.Debug(Tag, $"Map objects: trees={treeCells}, borders={borderCells}");

            // Start a new round: players, starting resources and population.
            game = new Game(gameMap, 42);
            game.StartGame();
            Log.Debug(Tag, $"Round started: {game.Players.Count} players, target population {game.TargetPopulation}");
            Log.Debug(Tag, $"Stacks: {game.Stacks.Count}, collectors: {game.Collectors.Count}");

            // Demo: place a construction site so the building feature is visible.
            var demoBuilding = game.PlaceDemoBuilding(BuildingType.Sawmill);
            Log.Debug(Tag, demoBuilding != null
                ? $"Demo construction site: {demoBuilding.Type} at {demoBuilding.Position}"
                : "No position for a demo construction site found.");

            int size = map.WidthHeight;
            cameraOffset = new Vector2(-(size - 1) * 4f, -(size - 1) * 4.5f);

            // Fit the whole map into the window.
            float mapWidth = (size - 1) * 24f;
            float mapHeight = (size - 1) * 9f;
            zoom = MathF.Min(view.FramebufferSize.X / mapWidth, view.FramebufferSize.Y / mapHeight);

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

        // Advance the simulation (50 ticks per second).
        game?.TickOnce();

        // Orthographic projection centered on the origin: the map center
        // (translated to (0,0) by the camera offset) lands in the middle of
        // the screen. Y grows downward (bottom=+h/2, top=-h/2) to match the
        // map geometry.
        var projection = Matrix4x4.CreateOrthographicOffCenter(-width / 2, width / 2, height / 2, -height / 2, -1, 1);
        var viewMatrix = Matrix4x4.CreateTranslation(cameraOffset.X * zoom, cameraOffset.Y * zoom, 0);
        terrain.Draw(viewMatrix * projection, zoom);

        if (gameObjects != null)
        {
            gameObjects.Begin();
            CollectGameObjects();
            gameObjects.Draw(viewMatrix * projection, zoom);
        }

        minimap?.Draw(width - 8f - 160f, 8f, 160f, 160f, width, height);
        hud?.Draw(width, height, BuildHudLines());

        var error = gl.GetError();
        if (error != GLEnum.NoError)
            Log.Error(Tag, $"GL error: {error}");
    }

    /// <summary>Builds the HUD text lines (resources, population, menu).</summary>
    static List<string> BuildHudLines()
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

        if (game.IsWon)
            lines.Add("YOU WIN! POPULATION TARGET REACHED");

        return lines;
    }

    /// <summary>Collects the sprites of the game objects for the current frame.</summary>
    static void CollectGameObjects()
    {
        if (gameMap == null || gameObjects == null)
            return;

        int size = gameMap.Size;

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

        // Settlers walking around the map. The animation frame follows the
        // game clock (or wall time before the game starts).
        int frame = (int)((game?.Tick ?? (ulong)System.Environment.TickCount) / 10) % 8;
        if (game != null)
        {
            foreach (var settler in game.Settlers)
            {
                if (!gameMap.IsInBounds(settler.Position))
                    continue;
                if (settler.Type == SerfType.Collector)
                    gameObjects.AddCarrier(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.CarriedGood, settler.Facing);
                else
                    gameObjects.AddMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Facing);
            }
        }
        else
        {
            foreach (var settler in gameMap.Settlers)
            {
                var pos = new Position(settler.PosX, settler.PosY);
                if (!gameMap.IsInBounds(pos))
                    continue;
                gameObjects.AddMovable(pos, gameMap.GetCell(pos).Height, settler.Party, frame);
            }
        }

        // Carriers walking along the roads.
        if (game != null)
        {
            foreach (var carrier in game.Carriers)
            {
                if (!gameMap.IsInBounds(carrier.Position))
                    continue;
                gameObjects.AddCarrier(carrier.Position, gameMap.GetCell(carrier.Position).Height, carrier.Player, frame, carrier.CarriedGood, carrier.Facing);
                if (carrier.IsCarrying && carrier.CarriedGood.HasValue && !GameObjectRenderer.CarrierSequences.ContainsKey(carrier.CarriedGood.Value))
                    gameObjects.AddCarriedGood(carrier.Position, gameMap.GetCell(carrier.Position).Height, carrier.CarriedGood.Value);
            }
        }

        // Resource stacks on the map.
        if (game != null)
        {
            foreach (var stack in game.Stacks)
            {
                if (stack.IsEmpty || !gameMap.IsInBounds(stack.Position))
                    continue;
                gameObjects.AddStack(stack.Position, gameMap.GetCell(stack.Position).Height, stack.Material, stack.Count);
            }
        }

        // Carried goods above the collectors (only for goods without a
        // dedicated carrying animation).
        if (game != null)
        {
            foreach (var collector in game.Collectors)
            {
                if (!collector.IsCarrying || !gameMap.IsInBounds(collector.Position))
                    continue;
                if (!GameObjectRenderer.CarrierSequences.ContainsKey(collector.CarriedGood!.Value))
                    gameObjects.AddCarriedGood(collector.Position, gameMap.GetCell(collector.Position).Height, collector.CarriedGood.Value);
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = gameMap.GetCell(x, y);
                var pos = new Position(x, y);

                if (cell.Road == RoadState.Road)
                    gameObjects.AddRoad(pos, cell.Height);
                if (cell.HasFlag)
                    gameObjects.AddFlag(pos, cell.Height);
            }
        }

        // Trees and other map objects.
        ulong tick = game?.Tick ?? 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = gameMap.GetCell(x, y);
                if (GameObjectRenderer.IsTreeObject(cell.MapObject))
                    gameObjects.AddTree(new Position(x, y), cell.Height, tick);
            }
        }

        // Player border posts: drawn where the territory owner changes
        // between neighbouring cells.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = gameMap.GetCell(x, y);
                if (cell.Player == 0)
                    continue;

                bool isBorder = false;
                foreach (var direction in MapGeometry.AllDirections())
                {
                    var neighbor = new Position(x, y).GetNeighbor(direction);
                    if (!gameMap.IsInBounds(neighbor))
                        continue;
                    if (gameMap.GetCell(neighbor).Player != cell.Player)
                    {
                        isBorder = true;
                        break;
                    }
                }

                if (isBorder)
                    gameObjects.AddBorder(new Position(x, y), cell.Height, cell.Player);
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

    static void Window_Resize(Vector2D<int> size)
    {
        gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    }

    static void Window_Closing()
    {
        hud?.Dispose();
        hud = null;
        minimap?.Dispose();
        minimap = null;
        gameObjects?.Dispose();
        gameObjects = null;
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
