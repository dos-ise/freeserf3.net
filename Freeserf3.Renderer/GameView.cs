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
    int minimapUpdateCounter;
    ControlPanel? controlPanel;
    MainMenu? menu;
    Map? gameMap;
    Game? game;
    FogOfWar? fogOfWar;
    int fogUpdateCounter;

    // Camera state.
    Vector2 cameraOffset;
    float zoom = 1.0f;

    // Interaction state.
    InteractionMode mode = InteractionMode.Select;
    BuildingType buildType = BuildingType.Lumberjack;
    Position? selected;
    Serf? selectedSoldier;
    Vector2? mousePosition;
    int lastScreenWidth, lastScreenHeight;

    // Map to start on the next render (the GL context is only current in
    // the render loop, so the game cannot be started from a click handler).
    string? pendingMapFile;

    // Sound: the host provides the platform player; the managers decode
    // the game's sound file and play the music tracks.
    ISoundPlayer? soundPlayer;
    SoundManager? soundManager;
    MusicManager? musicManager;

    public GameView(GameData gameData, Action<string>? log = null)
    {
        this.gameData = gameData;
        this.log = log ?? (_ => { });
    }

    /// <summary>
    /// Sets the platform sound player and loads the sound effects and the
    /// music tracks. Called by the host before the game starts.
    /// </summary>
    public void SetSoundPlayer(ISoundPlayer player)
    {
        soundPlayer = player;

        var reader = gameData.LoadSound();
        if (reader != null)
        {
            reader.Initialize();
            soundManager = new SoundManager(reader, player);
            log($"Sounds: {reader.SequenceCount} sequences loaded");
        }
        else
        {
            log("Sound file not found; sound effects disabled.");
        }

        musicManager = new MusicManager(player, gameData.ThemePath);
        musicManager.Load();
        log($"Music: {musicManager.TrackCount} tracks found");
    }

    /// <summary>The running game, or null if not loaded.</summary>
    public Game? Game => game;

    /// <summary>The current interaction mode.</summary>
    public InteractionMode Mode => mode;

    /// <summary>The building type placed in build mode.</summary>
    public BuildingType BuildType => buildType;

    /// <summary>Whether the in-game menu is currently open.</summary>
    public bool IsMenuOpen => controlPanel?.SystemMenuVisible == true;

    /// <summary>Whether the simulation is paused (in-game menu).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Game speed multiplier (1..5), like settlers-remake.</summary>
    public int GameSpeed { get; private set; } = 1;

    /// <summary>Raised when the player chooses to exit the game.</summary>
    public event Action? ExitRequested;

    /// <summary>Whether the start menu is currently shown.</summary>
    public bool IsInMenu => menu != null && game == null;

    /// <summary>
    /// Loads the game data and shows the start menu. The game itself is
    /// started when the player picks a map. Returns false if the game data
    /// could not be loaded.
    /// </summary>
    public bool Load(string? mapFile, int screenWidth, int screenHeight)
    {
        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;

        // The start menu is shown first; the game starts on map selection.
        menu = new MainMenu(State.Gl);
        menu.Load(gameData);
        menu.SinglePlayerSelected += () => menu.ShowMapSelection();
        menu.MapSelected += map => pendingMapFile = map;
        menu.MultiplayerSelected += () => log("Multiplayer not implemented yet.");
        menu.OptionsSelected += () => log("Options not implemented yet.");
        menu.ExitRequested += () => ExitRequested?.Invoke();

        // If a map was given on the command line, start it directly.
        if (mapFile != null)
            pendingMapFile = mapFile;

        State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        return true;
    }

    /// <summary>
    /// Loads the given map, creates the renderers and starts a new round.
    /// The camera is centered on the map and fitted to the given screen
    /// size. Returns false if the map could not be loaded.
    /// </summary>
    public bool StartGame(string mapFile)
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

        // Game objects: buildings (dat 13), settlers (dat 10), soldiers
        // (dat 12), workers (dat 11, bricklayer/digger actions), roads,
        // flags.
        var buildingsGfx = gameData.LoadGfxFile(GameObjectRenderer.BuildingsFile);
        var settlersGfx = gameData.LoadGfxFile(GameObjectRenderer.SettlersFile);
        var soldiersGfx = gameData.LoadGfxFile(GameObjectRenderer.SoldiersFile);
        var workersGfx = gameData.LoadGfxFile(GameObjectRenderer.WorkersFile);
        var objectsGfx = gameData.LoadGfxFile(GameObjectRenderer.ObjectsFile);
        if (buildingsGfx != null && settlersGfx != null)
        {
            gameObjects = new GameObjectRenderer(State.Gl);
            var playerColors = map.Settlers.Select(s => (int)s.Party)
                .Concat(map.Buildings.Select(b => (int)b.Party))
                .Distinct();
            gameObjects.Load(buildingsGfx, settlersGfx, soldiersGfx, playerColors);
            if (workersGfx != null)
                gameObjects.LoadWorkers(workersGfx, playerColors);
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

        // Fog of war: the map starts unexplored and is revealed by the
        // player's buildings and settlers. The fog darkens the terrain and
        // hides map objects (like settlers-remake).
        fogOfWar = new FogOfWar(gameMap);
        terrain.UpdateFog(fogOfWar);
        gameObjects?.SetFog(fogOfWar);

        // Left-side control panel (building selection, system button).
        controlPanel = new ControlPanel(State.Gl);
        controlPanel.Load(gameData);
        controlPanel.BuildingSelected += OnBuildingSelected;
        controlPanel.SystemButtonClicked += OnSystemButtonClicked;
        controlPanel.PauseRequested += OnPauseRequested;
        controlPanel.MusicRequested += OnMusicRequested;
        controlPanel.SaveRequested += () => log("Save not implemented yet.");
        controlPanel.ExitRequested += OnMenuExitRequested;
        controlPanel.CancelRequested += OnCancelRequested;
        controlPanel.SpeedUpRequested += OnSpeedUpRequested;
        controlPanel.SpeedDownRequested += OnSpeedDownRequested;

        // Start a new round: players, starting resources and population.
        game = new Game(gameMap, 42);
        game.PlaySound = id => soundManager?.PlaySound(id);
        game.StartGame();
        // The map buildings stand on sand; rebuild the terrain so the
        // ground under them is sand instead of grass.
        terrain.Rebuild(gameMap);
        terrain.UpdateFog(fogOfWar);
        log($"Round started: {game.Players.Count} players, target population {game.TargetPopulation}");
        log($"Stacks: {game.Stacks.Count}, collectors: {game.Collectors.Count}");

        // The control panel shows the human player's data.
        controlPanel.Player = game.GetPlayer(0);
        controlPanel.Game = game;
        controlPanel.GameSpeed = GameSpeed;

        // Start the music.
        musicManager?.Start();

        // Center the camera on the player's base (their first building, or
        // the start position if they have none).
        var start = game.GetPlayer(0).StartPosition;
        var basePosition = game.Buildings
            .Where(b => b.Player == 0 && b.State != BuildingState.Destroyed)
            .OrderBy(b => MapGeometry.GetDistance(b.Position, start))
            .Select(b => (Position?)b.Position)
            .FirstOrDefault() ?? start;
        CenterOnMap(lastScreenWidth, lastScreenHeight, basePosition);
        State.Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        menu = null;
        return true;
    }

    /// <summary>
    /// Centers the camera on the given position (or the map center) and
    /// fits the map to the given screen size.
    /// </summary>
    public void CenterOnMap(int screenWidth, int screenHeight, Position? center = null)
    {
        if (gameMap == null)
            return;

        int size = gameMap.Size;

        // Center the camera on the player's start (or the map center) so
        // the revealed fog of war area is visible at game start.
        var target = center ?? new Position(size / 2, size / 2);
        var (cx, cy) = MapGeometry.GridToScreen(target, 0f);
        cameraOffset = new Vector2(-cx, -cy);

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
        State.Gl.Viewport(0, 0, (uint)screenWidth, (uint)screenHeight);
        State.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // The start menu is drawn instead of the game while it is shown.
        if (menu != null)
        {
            // Start the selected map now that the GL context is current.
            if (pendingMapFile != null)
            {
                string map = pendingMapFile;
                pendingMapFile = null;
                StartGame(map);
            }
            else
            {
                menu.Draw(screenWidth, screenHeight);
                return;
            }
        }

        if (terrain == null)
            return;

        // Advance the simulation (once per frame per speed step), unless paused.
        if (!IsPaused)
        {
            for (int i = 0; i < GameSpeed; i++)
                game?.TickOnce();
            if (++minimapUpdateCounter >= 30)
            {
                minimapUpdateCounter = 0;
                if (game != null)
                    minimap?.Update(game);
            }

            // The fog of war is updated a few times per second.
            if (++fogUpdateCounter >= 15)
            {
                fogUpdateCounter = 0;
                if (game != null && fogOfWar != null)
                {
                    fogOfWar.Update(game);
                    terrain.UpdateFog(fogOfWar);
                }
            }
        }

        // Advance to the next music track when the current one finished.
        musicManager?.Update();

        var viewProjection = CreateViewProjection(screenWidth, screenHeight);
        terrain.Draw(viewProjection, zoom);

        if (gameObjects != null)
        {
            gameObjects.Begin();
            CollectGameObjects();

            // Building preview (ghost) while in build mode.
            if (mode == InteractionMode.Build && mousePosition is Vector2 mouse && gameMap != null)
            {
                var pos = MouseToGrid(mouse, screenWidth, screenHeight);
                if (gameMap.IsInBounds(pos))
                    gameObjects.AddBuildingGhost(buildType, pos, gameMap.GetCell(pos).Height, CanPlaceBuilding(pos));
            }

            gameObjects.Draw(viewProjection, zoom);
        }

        // The fog of war is baked into the terrain color and the game
        // objects, so nothing extra is drawn here.

        // Minimap on top of the control panel, then the panel itself.
        var (mx, my, mw, mh) = controlPanel.GetMiniMapRect(screenWidth, screenHeight);
        minimap?.Draw(mx, my, mw, mh, screenWidth, screenHeight);
        controlPanel.MousePosition = mousePosition;
        controlPanel.Draw(screenWidth, screenHeight);

        // Game-over overlay when the round is won or lost.
        if (game != null && (game.IsWon || game.IsDefeated))
            controlPanel.DrawGameOver(game.IsWon, screenWidth, screenHeight);
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

    /// <summary>Updates the pointer position (used for the building preview).</summary>
    public void SetMousePosition(Vector2 position)
    {
        mousePosition = position;
    }

    /// <summary>
    /// Handles a left click at the given screen position: select or place
    /// depending on the current mode.
    /// </summary>
    public void HandleClick(Vector2 position, int screenWidth, int screenHeight)
    {
        // Clicks on the start menu are handled by the menu.
        if (menu != null)
        {
            menu.HandleClick(position);
            return;
        }

        if (gameMap == null || IsMenuOpen)
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

    /// <summary>
    /// Handles a right click: back to select mode, or order the selected
    /// soldier to attack/march to the clicked position.
    /// </summary>
    public void HandleRightClick(Vector2 position, int screenWidth, int screenHeight)
    {
        if (mode != InteractionMode.Select)
        {
            mode = InteractionMode.Select;
            controlPanel?.ClearSelection();
            log("Mode: select");
            return;
        }

        // A selected soldier is ordered to the clicked position: attack an
        // enemy settler or building there, otherwise march to it.
        if (selectedSoldier != null && game != null && gameMap != null)
        {
            var pos = MouseToGrid(position, screenWidth, screenHeight);
            if (gameMap.IsInBounds(pos))
            {
                OrderSelectedSoldier(pos);
                return;
            }
        }

        // Deselect the selected building.
        selected = null;
        selectedSoldier = null;
        if (controlPanel != null)
            controlPanel.SelectedMapBuilding = null;
    }

    /// <summary>
    /// Orders the selected soldier to the given position: attack the enemy
    /// settler or building there, otherwise march to it.
    /// </summary>
    void OrderSelectedSoldier(Position pos)
    {
        var soldier = selectedSoldier;
        if (soldier == null || game == null)
            return;

        // Attack an enemy settler at the position.
        foreach (var settler in game.Settlers)
        {
            if (settler.Player == soldier.Player || settler.IsDead)
                continue;
            if (settler.Position != pos)
                continue;

            soldier.AttackTarget = settler;
            soldier.AttackBuilding = null;
            soldier.MarchTarget = null;
            soldier.OrderedAttack = true;
            soldier.SoldierState = SoldierState.Attacking;
            log($"Soldier ordered to attack {settler.Type} at {pos}");
            return;
        }

        // Attack an enemy building at the position.
        foreach (var building in game.Buildings)
        {
            if (building.Player == soldier.Player || building.State == BuildingState.Destroyed)
                continue;
            if (!IsInBuildingFootprint(building, pos))
                continue;

            soldier.AttackBuilding = building;
            soldier.AttackTarget = null;
            soldier.MarchTarget = null;
            soldier.OrderedAttack = true;
            soldier.SoldierState = SoldierState.Attacking;
            log($"Soldier ordered to attack {building.Type} at {pos}");
            return;
        }

        // Otherwise march to the position.
        soldier.AttackTarget = null;
        soldier.AttackBuilding = null;
        soldier.MarchTarget = pos;
        soldier.OrderedAttack = false;
        soldier.SoldierState = SoldierState.Attacking;
        log($"Soldier ordered to march to {pos}");
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
        bool handled = controlPanel?.HandleClick(position) == true;
        if (handled)
            soundManager?.PlaySound(62); // UI click
        return handled;
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
        else
        {
            selected = null;
            selectedSoldier = null;
            if (controlPanel != null)
                controlPanel.SelectedMapBuilding = null;
        }
    }

    /// <summary>Releases all renderer resources.</summary>
    public void Dispose()
    {
        menu?.Dispose();
        menu = null;
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
        selected = null;
        selectedSoldier = null;
        if (controlPanel != null)
            controlPanel.SelectedMapBuilding = null;
        soundManager?.PlaySound(62); // UI click
        log($"Mode: build ({buildType})");
    }

    /// <summary>The Music button toggles the music.</summary>
    void OnMusicRequested()
    {
        if (soundPlayer == null)
        {
            log("Music not available.");
            return;
        }

        soundPlayer.MusicEnabled = !soundPlayer.MusicEnabled;
        if (soundPlayer.MusicEnabled)
        {
            musicManager?.Start();
            log("Music on");
        }
        else
        {
            musicManager?.Stop();
            log("Music off");
        }
    }

    /// <summary>
    /// The system (computer) button toggles the in-game menu. Opening the
    /// menu cancels any build mode and pauses the game; closing it resumes.
    /// </summary>
    void OnSystemButtonClicked()
    {
        if (controlPanel?.SystemMenuVisible == true)
        {
            if (mode != InteractionMode.Select)
            {
                mode = InteractionMode.Select;
                controlPanel.ClearSelection();
            }
            IsPaused = true;
            controlPanel.IsPaused = true;
            log("In-game menu opened (paused)");
        }
        else
        {
            IsPaused = false;
            controlPanel.IsPaused = false;
            log("In-game menu closed (resumed)");
        }
    }

    /// <summary>The Pause button toggles the pause state.</summary>
    void OnPauseRequested()
    {
        IsPaused = !IsPaused;
        if (controlPanel != null)
            controlPanel.IsPaused = IsPaused;
        log(IsPaused ? "Game paused" : "Game resumed");
    }

    /// <summary>The Cancel button closes the menu and resumes the game.</summary>
    void OnCancelRequested()
    {
        controlPanel?.CloseSystemMenu();
        IsPaused = false;
        controlPanel.IsPaused = false;
        log("In-game menu closed (resumed)");
    }

    /// <summary>The Exit button requests to leave the game.</summary>
    void OnMenuExitRequested()
    {
        log("Exit requested");
        ExitRequested?.Invoke();
    }

    void OnSpeedUpRequested()
    {
        if (GameSpeed < 5)
        {
            GameSpeed++;
            if (controlPanel != null)
                controlPanel.GameSpeed = GameSpeed;
            log($"Game speed: {GameSpeed}x");
        }
    }

    void OnSpeedDownRequested()
    {
        if (GameSpeed > 1)
        {
            GameSpeed--;
            if (controlPanel != null)
                controlPanel.GameSpeed = GameSpeed;
            log($"Game speed: {GameSpeed}x");
        }
    }

    /// <summary>Closes the in-game menu (e.g. via the Escape key).</summary>
    public void CloseMenu()
    {
        if (IsMenuOpen)
        {
            controlPanel?.CloseSystemMenu();
            IsPaused = false;
            if (controlPanel != null)
                controlPanel.IsPaused = false;
            log("In-game menu closed (resumed)");
        }
    }

    /// <summary>Collects the sprites of the game objects for the current frame.</summary>
    void CollectGameObjects()
    {
        if (gameMap == null || gameObjects == null)
            return;

        int size = gameMap.Size;

        // Buildings from the game simulation. The map-file buildings are
        // loaded into the game at start, so they are drawn here too.
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
        // settler's own movement (standing frame while idle).
        if (game != null)
        {
            foreach (var settler in game.Settlers)
            {
                if (!gameMap.IsInBounds(settler.Position))
                    continue;
                int frame = GetMovableFrame(settler);
                bool idle = !settler.IsWalking;
                var screenPos = GetMovableScreenPos(settler);
                if (settler.Type == SerfType.Collector)
                    gameObjects.AddCarrier(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.CarriedGood, settler.Facing, idle, screenPos);
                else if (settler.Type == SerfType.Soldier)
                {
                    bool attacking = settler.SoldierState == SoldierState.Attacking
                        && (settler.AttackTarget != null || settler.AttackBuilding != null);
                    if (attacking)
                        gameObjects.AddSoldierAttacking(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, settler.AttackAnimationTicks / 5, settler.Weapon, settler.Facing, screenPos);
                    else
                        gameObjects.AddSoldier(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Weapon, settler.Facing, idle, screenPos);
                }
                else if (settler.Type == SerfType.Builder)
                    gameObjects.AddBuilderMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Facing, settler.BuilderState == BuilderState.Building, idle, screenPos);
                else if (settler.Type == SerfType.ResourceWorker)
                {
                    // The worker is inside the house between work cycles.
                    if (settler.ResourceState == ResourceWorkerState.InsideHome)
                        continue;
                    if (settler.ResourceState == ResourceWorkerState.Working)
                        gameObjects.AddWorkingMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Facing, settler.Profession);
                    else if (settler.IsCarrying)
                        gameObjects.AddCarryingMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Profession);
                    else
                        gameObjects.AddWorkerMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Facing, settler.Profession, idle, screenPos);
                }
                else
                    gameObjects.AddMovable(settler.Position, gameMap.GetCell(settler.Position).Height, settler.Player, frame, settler.Facing, idle, screenPos);
            }
        }
        else
        {
            int frame = (int)(Environment.TickCount / 10) % 8;
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
                int frame = GetMovableFrame(carrier);
                bool idle = !carrier.IsWalking;
                var screenPos = GetMovableScreenPos(carrier);
                gameObjects.AddCarrier(carrier.Position, gameMap.GetCell(carrier.Position).Height, carrier.Player, frame, carrier.CarriedGood, carrier.Facing, idle, screenPos);
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

        // Trees and stones still on the map (cut/mined ones disappear).
        ulong tick = game?.Tick ?? 0;
        if (game != null)
        {
            foreach (var tree in game.Trees)
            {
                if (!gameMap.IsInBounds(tree))
                    continue;
                gameObjects.AddTree(tree, gameMap.GetCell(tree).Height, tick);
            }

            foreach (var stone in game.Stones)
            {
                if (!gameMap.IsInBounds(stone))
                    continue;
                gameObjects.AddStone(stone, gameMap.GetCell(stone).Height, 1);
            }
        }
        else
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var cell = gameMap.GetCell(x, y);
                    if (GameObjectRenderer.IsTreeObject(cell.MapObject))
                        gameObjects.AddTree(new Position(x, y), cell.Height, tick);
                }
            }
        }

        // Player border posts: drawn on land cells where the territory ends
        // - at the coast (a water neighbour) or at another player's land.
        // The territory is claimed by the military buildings (dynamic),
        // like settlers-remake. Water cells themselves never get a border
        // post (like settlers-remake, where blocked/sea cells are skipped).
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var pos = new Position(x, y);
                var cell = gameMap.GetCell(x, y);
                int owner = game?.GetTerritoryPlayer(pos) ?? cell.Player;
                if (game != null && !game.HasTerritoryOwner(pos))
                    continue;
                if (owner == 0 && game == null)
                    continue;
                if (LandscapeMapping.IsWater(cell.Landscape))
                    continue;

                bool isBorder = false;
                foreach (var direction in MapGeometry.AllDirections())
                {
                    var neighbor = pos.GetNeighbor(direction);
                    if (!gameMap.IsInBounds(neighbor))
                        continue;
                    var neighborCell = gameMap.GetCell(neighbor);
                    if (LandscapeMapping.IsWater(neighborCell.Landscape))
                    {
                        // The territory ends at the coast.
                        isBorder = true;
                        break;
                    }
                    int neighborOwner = game?.GetTerritoryPlayer(neighbor) ?? neighborCell.Player;
                    bool neighborClaimed = game == null || game.HasTerritoryOwner(neighbor);
                    if (neighborOwner != owner || !neighborClaimed)
                    {
                        isBorder = true;
                        break;
                    }
                }

                if (isBorder)
                    gameObjects.AddBorder(pos, cell.Height, owner);
            }
        }

        // Selection marker.
        if (selected is Position sel && gameMap.IsInBounds(sel))
        {
            gameObjects.AddSelectionMarker(sel, gameMap.GetCell(sel).Height);
        }

        // Selected soldier marker.
        if (selectedSoldier != null)
        {
            if (selectedSoldier.IsDead || game == null || !game.Settlers.Contains(selectedSoldier))
                selectedSoldier = null;
            else if (gameMap.IsInBounds(selectedSoldier.Position))
                gameObjects.AddSelectionMarker(selectedSoldier.Position, gameMap.GetCell(selectedSoldier.Position).Height);
        }
    }

    /// <summary>
    /// Maps a map-file building type byte to a game building type. The map
    /// file uses the original game order (see settlers-remake
    /// EOriginalMapBuildingType).
    /// </summary>
    static BuildingType? BuildingTypeFromMap(byte buildingType)
    {
        return Game.BuildingTypeFromMap(buildingType);
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

    /// <summary>
    /// The screen position of a movable, interpolated between the previous
    /// and the current cell while it walks (smooth movement instead of
    /// cell-to-cell jumps).
    /// </summary>
    (float X, float Y) GetMovableScreenPos(Serf settler)
    {
        if (settler.IsWalking && settler.PreviousPosition is Position prev)
        {
            float t = settler.MoveProgress;
            var curr = settler.Position;
            var (px, py) = MapGeometry.GridToScreen(prev, gameMap!.GetCell(prev).Height);
            var (cx, cy) = MapGeometry.GridToScreen(curr, gameMap.GetCell(curr).Height);
            return (px + (cx - px) * t, py + (cy - py) * t);
        }
        return MapGeometry.GridToScreen(settler.Position, gameMap!.GetCell(settler.Position).Height);
    }

    /// <summary>
    /// The animation frame of a movable: advances while walking, standing
    /// frame (0) while idle.
    /// </summary>
    static int GetMovableFrame(Serf settler)
    {
        return settler.IsWalking ? (int)(settler.MoveProgress * 11) % 11 : 0;
    }

    /// <summary>Selects the soldier or building at the given position, if any.</summary>
    void SelectAt(Position pos)
    {
        selected = null;
        selectedSoldier = null;
        controlPanel.SelectedMapBuilding = null;

        // A soldier of the human player is selected first.
        if (game != null)
        {
            foreach (var settler in game.Settlers)
            {
                if (settler.Player != 0 || settler.Type != SerfType.Soldier)
                    continue;
                if (settler.Position != pos)
                    continue;

                selectedSoldier = settler;
                log($"Selected soldier ({settler.Weapon}) at {settler.Position}");
                return;
            }
        }

        if (game != null)
        {
            foreach (var building in game.Buildings)
            {
                if (IsInBuildingFootprint(building, pos))
                {
                    selected = building.Position;
                    controlPanel.SelectedMapBuilding = building;
                    log($"Selected {building.Type} at {building.Position} ({building.State})");
                    return;
                }
            }
        }

        foreach (var building in gameMap!.Buildings)
        {
            var type = BuildingTypeFromMap(building.BuildingType);
            if (type.HasValue && IsInBuildingFootprint(type.Value, new Position(building.PosX, building.PosY), pos))
            {
                selected = new Position(building.PosX, building.PosY);
                log($"Selected {(type?.ToString() ?? "building")} at ({building.PosX},{building.PosY})");
                return;
            }
        }
    }

    /// <summary>Whether the position lies within the building's footprint.</summary>
    static bool IsInBuildingFootprint(Building building, Position pos)
    {
        return IsInBuildingFootprint(building.Type, building.Position, pos);
    }

    /// <summary>Whether the position lies within the building's footprint.</summary>
    static bool IsInBuildingFootprint(BuildingType type, Position building, Position pos)
    {
        foreach (var (dx, dy) in BuildingFootprints.For(type))
        {
            if (building.X + dx == pos.X && building.Y + dy == pos.Y)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cycles the tool the selected toolsmith produces. The toolsmith can
    /// produce any of the six tools; the selection is shown in the log.
    /// Returns true if a toolsmith was selected and cycled.
    /// </summary>
    public bool CycleToolsmithOutput()
    {
        if (game == null || selected is not Position pos)
            return false;

        var building = game.Buildings.FirstOrDefault(b => IsInBuildingFootprint(b, pos));
        if (building == null || building.Type != BuildingType.Toolsmith)
            return false;

        var tools = new[] { Material.Hammer, Material.Axe, Material.Pick, Material.Saw, Material.Scythe, Material.FishingRod };
        int index = Array.IndexOf(tools, building.ToolsmithOutput);
        building.ToolsmithOutput = tools[(index + 1) % tools.Length];
        log($"Toolsmith now produces {building.ToolsmithOutput}");
        return true;
    }

    /// <summary>
    /// Demolishes the selected building. Half of the construction
    /// materials go back to the stock and the workers are released.
    /// Returns true if a building was demolished.
    /// </summary>
    public bool DemolishSelectedBuilding()
    {
        if (game == null || selected is not Position pos)
            return false;

        var building = game.Buildings.FirstOrDefault(b => IsInBuildingFootprint(b, pos));
        if (building == null || building.State == BuildingState.Destroyed)
            return false;

        game.DemolishBuilding(building);
        selected = null;
        selectedSoldier = null;
        controlPanel.SelectedMapBuilding = null;
        log($"Demolished {building.Type} at {pos}");
        return true;
    }

    /// <summary>Places the current building type at the given position.</summary>
    void PlaceBuilding(Position pos)
    {
        if (gameMap == null || game == null)
            return;

        if (!CanPlaceBuilding(pos))
        {
            log("Cannot build here.");
            return;
        }

        var building = game.PlaceBuilding(buildType, pos, 0);
        // The new building stands on sand; rebuild the terrain so the
        // ground under it is sand instead of grass.
        terrain.Rebuild(gameMap);
        terrain.UpdateFog(fogOfWar);
        soundManager?.PlaySound(51); // building construction
        log($"Placed {building.Type} at {pos} ({building.State})");
    }

    /// <summary>
    /// Whether a building can be placed at the given position: in bounds,
    /// walkable and on suitable terrain for the building type (mines on
    /// mountains, farms on fertile ground, fishers near water, living
    /// houses on own territory).
    /// </summary>
    bool CanPlaceBuilding(Position pos)
    {
        if (gameMap == null || game == null)
            return false;
        return game.CanPlaceBuilding(buildType, pos, 0);
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

