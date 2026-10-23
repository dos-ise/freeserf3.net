/*
 * GameView.cs - Shared game view for the Windows and Android hosts
 *
 * Encapsulates everything the desktop and Android hosts have in common:
 * map loading, game setup, the render loop, object collection, camera
 * state and the map interactions (select, build, road, flag). The hosts
 * only create the window and forward input events to this class.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Simulation;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>The interaction modes of the map view.</summary>
public enum InteractionMode
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

/// <summary>
/// The shared game view: owns the map, the simulation, the renderers and
/// the camera, and draws one frame per call to <see cref="Render"/>.
/// </summary>
public sealed class GameView : IDisposable
{
    readonly GameData gameData;
    readonly Action<string> log;

    TerrainRenderer? terrain;
    GameObjectRenderer? gameObjects;
    Minimap? minimap;
    ControlPanel? controlPanel;
    Map? gameMap;
    Game? game;

    // Camera state.
    Vector2 cameraOffset;
    float zoom = 1.0f;

    // Interaction state.
    InteractionMode mode = InteractionMode.Select;
    BuildingType buildType = BuildingType.Lumberjack;
    Position? selected;

    public GameView(GameData gameData, Action<string>? log = null)
    {
        this.gameData = gameData;
        this.log = log ?? (_ => { });
    }

    /// <summary>The running game, or null if not loaded.</summary>
    public Game? Game => game;

    /// <summary>The current interaction mode.</summary>
    public InteractionMode Mode => mode;

    /// <summary>The building type placed in build mode.</summary>
    public BuildingType BuildType => buildType;

    /// <summary>
    /// Loads the map, creates the renderers and starts a new round. The
    /// camera is centered on the map and fitted to the given screen size.
    /// Returns false if the map or the game data could not be loaded.
    /// </summary>
    public bool Load(string? mapFile, int screenWidth, int screenHeight)
    {
        if (mapFile == null)
        {
            mapFile = gameData.FindSinglePlayerMapFiles().FirstOrDefault();
        }

        if (mapFile == null)
        {
            log("No map file found in the game data.");
            return false;
        }

        log($"Loading map: {mapFile}");
        var map = gameData.LoadMap(mapFile);
        map.LoadMapResources();
        map.ReadBasicMapInformation();
        map.ReadMapData();
        map.ReadBuildings();
        map.ReadSettlers();
        map.ReadStacks();
        log($"Map: {map.WidthHeight}x{map.WidthHeight}, players={map.PlayerCount}, buildings={map.Buildings.Count}, settlers={map.Settlers.Count}");

        var gfx = gameData.LoadGfxFile(LandscapeMapping.LandscapeFile);
        if (gfx == null)
        {
            log("Landscape dat file not found.");
            return false;
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
            log($"Buildings: {map.Buildings.Count}, settlers: {map.Settlers.Count}");
        }
        else
        {
            log("Building/settler dat files not found; game objects disabled.");
        }

        gameMap = Map.FromReader(map);
        minimap = new Minimap(State.Gl, gameMap);

        // Left-side control panel (building selection, system button).
        controlPanel = new ControlPanel(State.Gl);
        controlPanel.Load(gameData);
        controlPanel.BuildingSelected += OnBuildingSelected;
        controlPanel.SystemButtonClicked += OnSystemButtonClicked;

        // Start a new round: players, starting resources and population.
        game = new Game(gameMap, 42);
        game.StartGame();
        log($"Round started: {game.Players.Count} players, target population {game.TargetPopulation}");
        log($"Stacks: {game.Stacks.Count}, collectors: {game.Collectors.Count}");

        // Demo: place a construction site so the building feature is visible.
        var demoBuilding = game.PlaceDemoBuilding(BuildingType.Sawmill);
        log(demoBuilding != null
            ? $"Demo construction site: {demoBuilding.Type} at {demoBuilding.Position}"
            : "No position for a demo construction site found.");

        CenterOnMap(screenWidth, screenHeight);
        State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        return true;
    }

    /// <summary>
    /// Centers the camera on the map and fits it to the given screen size.
    /// </summary>
    public void CenterOnMap(int screenWidth, int screenHeight)
    {
        if (gameMap == null)
            return;

        int size = gameMap.Size;

        // The map's screen-space bounding box is x: [-(size-1)*8, (size-1)*16],
        // y: [0, (size-1)*9]; its center is ((size-1)*4, (size-1)*4.5). The
        // projection is centered on the origin, so translating the map center
        // to (0,0) puts it in the middle of the screen.
        cameraOffset = new Vector2(-(size - 1) * 4f, -(size - 1) * 4.5f);

        // Fit the whole map into the window.
        float mapWidth = (size - 1) * 24f;
        float mapHeight = (size - 1) * 9f;
        zoom = MathF.Min(screenWidth / mapWidth, screenHeight / mapHeight);
    }

    /// <summary>
    /// Advances the simulation by one tick and draws the frame. The viewport
    /// is set to the given screen size.
    /// </summary>
    public void Render(int screenWidth, int screenHeight)
    {
        if (terrain == null)
            return;

        State.Gl.Viewport(0, 0, (uint)screenWidth, (uint)screenHeight);
        State.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Advance the simulation (once per frame).
        game?.TickOnce();

        var viewProjection = CreateViewProjection(screenWidth, screenHeight);
        terrain.Draw(viewProjection, zoom);

        if (gameObjects != null)
        {
            gameObjects.Begin();
            CollectGameObjects();
            gameObjects.Draw(viewProjection, zoom);
        }

        // Minimap on top of the control panel, then the panel itself.
        var (mx, my, mw, mh) = controlPanel.GetMiniMapRect(screenWidth, screenHeight);
        minimap?.Draw(mx, my, mw, mh, screenWidth, screenHeight);
        controlPanel.Draw(screenWidth, screenHeight);
    }

    /// <summary>Pans the camera by the given screen-space delta.</summary>
    public void Pan(Vector2 delta)
    {
        cameraOffset += delta / zoom;
    }

    /// <summary>Zooms the camera by the given factor (clamped).</summary>
    public void Zoom(float factor)
    {
        zoom = Math.Clamp(zoom * factor, 0.1f, 10.0f);
    }

    /// <summary>
    /// Handles a left click at the given screen position: select or place
    /// depending on the current mode.
    /// </summary>
    public void HandleClick(Vector2 position, int screenWidth, int screenHeight)
    {
        if (gameMap == null)
            return;

        var pos = MouseToGrid(position, screenWidth, screenHeight);
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
    public void HandleRightClick()
    {
        if (mode != InteractionMode.Select)
        {
            mode = InteractionMode.Select;
            controlPanel?.ClearSelection();
            log("Mode: select");
        }
    }

    /// <summary>Whether the position lies on the left-side control panel.</summary>
    public bool IsInPanel(Vector2 position, int screenWidth, int screenHeight)
    {
        if (controlPanel == null)
            return false;
        var (x, y, width, height) = controlPanel.GetMiniMapRect(screenWidth, screenHeight);
        return position.X >= x && position.X <= x + width && position.Y >= y && position.Y <= y + height;
    }

    /// <summary>
    /// Lets the control panel handle a click. Returns true if the panel
    /// consumed the click.
    /// </summary>
    public bool HandlePanelClick(Vector2 position)
    {
        return controlPanel?.HandleClick(position) == true;
    }

    /// <summary>Sets the interaction mode.</summary>
    public void SetMode(InteractionMode newMode)
    {
        mode = newMode;
        log($"Mode: {mode}");
    }

    /// <summary>Cycles to the next building type (build mode).</summary>
    public void NextBuilding()
    {
        var types = Enum.GetValues<BuildingType>();
        int index = (Array.IndexOf(types, buildType) + 1) % types.Length;
        buildType = types[index];
        log($"Build type: {buildType}");
    }

    /// <summary>Returns to select mode and clears the panel selection.</summary>
    public void CancelMode()
    {
        if (mode != InteractionMode.Select)
        {
            mode = InteractionMode.Select;
            controlPanel?.ClearSelection();
            log("Mode: select");
        }
    }

    /// <summary>Releases all renderer resources.</summary>
    public void Dispose()
    {
        controlPanel?.Dispose();
        controlPanel = null;
        minimap?.Dispose();
        minimap = null;
        gameObjects?.Dispose();
        gameObjects = null;
        terrain?.Dispose();
        terrain = null;
    }

    /// <summary>Selects a building in the control panel and enters build mode.</summary>
    void OnBuildingSelected(BuildingType type)
    {
        buildType = type;
        mode = InteractionMode.Build;
        log($"Mode: build ({buildType})");
    }

    /// <summary>The system (computer) button opens the in-game menu.</summary>
    void OnSystemButtonClicked()
    {
        log("System button clicked (in-game menu)");
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
                if (building.State == BuildingState.ConstructionSite)
                    gameObjects.AddConstructionSite(building.Type, building.Position, gameMap.GetCell(building.Position).Height);
                else
                    gameObjects.AddBuilding(building.Type, building.Position, gameMap.GetCell(building.Position).Height);
            }
        }

        // Settlers walking around the map. The animation frame follows the
        // game clock (or wall time before the game starts).
        int frame = (int)((game?.Tick ?? (ulong)Environment.TickCount) / 10) % 8;
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

        // Roads and flags from the cell state.
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

    Matrix4x4 CreateViewProjection(int screenWidth, int screenHeight)
    {
        // Orthographic projection centered on the origin: the map center
        // (translated to (0,0) by the camera offset) lands in the middle of
        // the screen. Y grows downward (bottom=+h/2, top=-h/2) to match the
        // map geometry.
        var projection = Matrix4x4.CreateOrthographicOffCenter(-screenWidth / 2, screenWidth / 2, screenHeight / 2, -screenHeight / 2, -1, 1);

        // Camera translation (in map pixels, scaled by zoom).
        var view = Matrix4x4.CreateTranslation(cameraOffset.X * zoom, cameraOffset.Y * zoom, 0);

        return view * projection;
    }

    /// <summary>Converts a screen position to a map grid position.</summary>
    Position MouseToGrid(Vector2 mousePos, int screenWidth, int screenHeight)
    {
        // The projection is centered on the origin, so the window center
        // corresponds to map pixel (0,0) before the camera offset.
        float mapX = (mousePos.X - screenWidth / 2f) / zoom - cameraOffset.X;
        float mapY = (mousePos.Y - screenHeight / 2f) / zoom - cameraOffset.Y;
        return MapGeometry.ScreenToGrid(mapX, mapY);
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
                    log($"Selected {building.Type} at {pos} ({building.State})");
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
                log($"Selected {(type?.ToString() ?? "building")} at {pos}");
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
            log("Cannot build on water.");
            return;
        }

        var building = game.PlaceBuilding(buildType, pos, 0);
        log($"Placed {building.Type} at {pos} ({building.State})");
    }

    /// <summary>Places a road cell at the given position.</summary>
    void PlaceRoad(Position pos)
    {
        if (gameMap == null)
            return;

        var modifier = new MapModifier(gameMap);
        modifier.PlaceRoad(pos);
        log($"Road at {pos}");
    }

    /// <summary>Places a flag at the given position.</summary>
    void PlaceFlag(Position pos)
    {
        if (gameMap == null)
            return;

        var modifier = new MapModifier(gameMap);
        modifier.SetFlag(pos, true);
        log($"Flag at {pos}");
    }
}
