/*
 * ControlPanel.cs - Left-side in-game control panel
 *
 * Draws the original Settlers 3 control panel on the left edge of the
 * screen: the minimap decoration on top, the building selection panel
 * below it (primary tabs, secondary tabs, building buttons) and the
 * system (computer) button plus the lower tab bar at the bottom.
 *
 * The layout and sprite indices follow settlers-remake (MIT)
 * MainPanel.java and ControlPanelLayoutProperties.java. The panel
 * background and minimap decoration are settler sequences of dat file 4
 * (one frame per resolution, we use the 768p frame and scale it), the
 * tab/system buttons are GUI images of dat file 3 and the building icons
 * are GUI images of dat file 14.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Render;
using Freeserf3.Simulation;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>
/// The left-side control panel of the game. Lets the player pick a
/// building to construct and provides the system (menu) button.
/// </summary>
public sealed class ControlPanel : IDisposable
{
    /// <summary>Tab and system button images are stored in dat file 3.</summary>
    public const int ButtonsFile = 3;

    /// <summary>Panel background and minimap decoration are in dat file 4.</summary>
    public const int PanelFile = 4;

    /// <summary>Building icons are stored in dat file 14.</summary>
    public const int BuildingIconsFile = 14;

    /// <summary>Material icons live in dat files 3, 14, 24 and 34.</summary>
    public const int MaterialIconsFile = 3;
    public const int MaterialIconsFile2 = 14;
    public const int MaterialIconsFile3 = 24;
    public const int MaterialIconsFile4 = 34;

    // GUI image indices in the buttons file (from settlers-remake MainPanel).
    const int TabBuildInactive = 51, TabBuildActive = 60;
    const int TabGoodsInactive = 54, TabGoodsActive = 63;
    const int TabSettlersInactive = 57, TabSettlersActive = 66;
    const int SubNormalInactive = 69, SubNormalActive = 81;
    const int SubFoodInactive = 72, SubFoodActive = 84;
    const int SubMilitaryInactive = 75, SubMilitaryActive = 87;
    const int SubSocialInactive = 78, SubSocialActive = 90;
    // Goods secondary tabs (STOCK, TOOLS, SPREAD, TRANSPORT).
    const int SubStockInactive = 258, SubStockActive = 270;
    const int SubToolsInactive = 261, SubToolsActive = 273;
    const int SubSpreadInactive = 264, SubSpreadActive = 276;
    const int SubTransportInactive = 267, SubTransportActive = 279;
    // Settlers secondary tabs (STATISTIC, PROFESSION, WARRIORS, PRODUCTION).
    const int SubStatisticInactive = 234, SubStatisticActive = 246;
    const int SubProfessionInactive = 237, SubProfessionActive = 249;
    const int SubWarriorsInactive = 240, SubWarriorsActive = 252;
    const int SubProductionInactive = 243, SubProductionActive = 255;
    const int SystemInactive = 93, SystemActive = 96;
    const int LowerScrollInactive = 111, LowerScrollActive = 99;
    const int LowerSwordsInactive = 114, LowerSwordsActive = 102;
    const int LowerSignpostInactive = 117, LowerSignpostActive = 105;
    const int LowerPotsInactive = 120, LowerPotsActive = 108;
    const int ActiveMark = 123;
    const int LabeledButtonInactive = 324, LabeledButtonActive = 327;

    // Settler sequences in the panel file.
    const int PanelBackgroundSequence = 2;
    const int MiniMapDecorationSequence = 0;
    const int HighResFrame = 2; // 768p layout

    // Layout in unscaled 768p pixels (y grows down).
    const float PanelWidth = 216f;
    const float PanelHeight = 544f;
    const float MiniMapHeight = 224f;
    const float TotalHeight = PanelHeight + MiniMapHeight;

    const float PrimaryTabWidth = 53f, PrimaryTabHeight = 55f, PrimaryTabSpacing = 17f, PrimaryTabLeft = 10f;
    const float SecondaryTabWidth = 45f, SecondaryTabHeight = 40f, SecondaryTabSpacing = 4f, SecondaryTabLeft = 10f;
    const float ContentLeft = 10f, ContentTop = 116f, ContentWidth = 197f, ContentHeight = 360f;
    const int ContentColumns = 2, ContentRows = 6;
    const float SystemLeft = 10f, SystemTop = 491f, SystemWidth = 30f, SystemHeight = 45f;
    const float LowerTabLeft = 45f, LowerTabTop = 491f, LowerTabSize = 40f;

    /// <summary>Building categories with their GUI sequence in dat file 14.</summary>
    static readonly (BuildingType Type, int Gui)[] NormalBuildings =
    {
        (BuildingType.Lumberjack, 0),
        (BuildingType.Sawmill, 6),
        (BuildingType.Stonecutter, 42),
        (BuildingType.Forester, 3),
        (BuildingType.Ironmine, 45),
        (BuildingType.Ironmelt, 24),
        (BuildingType.Goldmine, 48),
        (BuildingType.Goldmelt, 21),
        (BuildingType.Coalmine, 51),
        (BuildingType.Toolsmith, 30),
        (BuildingType.CharcoalBurner, 81)
    };

    static readonly (BuildingType Type, int Gui)[] FoodBuildings =
    {
        (BuildingType.Fisher, 18),
        (BuildingType.Farm, 69),
        (BuildingType.PigFarm, 66),
        (BuildingType.Mill, 15),
        (BuildingType.Slaughterhouse, 12),
        (BuildingType.Baker, 39),
        (BuildingType.Waterworks, 36),
        (BuildingType.DonkeyFarm, 87),
        (BuildingType.Winegrower, 90)
    };

    static readonly (BuildingType Type, int Gui)[] MilitaryBuildings =
    {
        (BuildingType.Tower, 54),
        (BuildingType.BigTower, 60),
        (BuildingType.Castle, 63),
        (BuildingType.LookoutTower, 9),
        (BuildingType.Weaponsmith, 27),
        (BuildingType.Barrack, 57),
        (BuildingType.Dockyard, 108),
        (BuildingType.Hospital, 93)
    };

    static readonly (BuildingType Type, int Gui)[] SocialBuildings =
    {
        (BuildingType.SmallLivinghouse, 72),
        (BuildingType.MediumLivinghouse, 75),
        (BuildingType.BigLivinghouse, 78),
        (BuildingType.Stock, 33),
        (BuildingType.MarketPlace, 99),
        (BuildingType.Harbor, 84),
        (BuildingType.Temple, 102),
        (BuildingType.BigTemple, 105)
    };

    /// <summary>
    /// The goods shown in the stock overview, in the settlers-remake
    /// MaterialInventoryLayout order (6 rows x 6 columns). Each entry is
    /// the material and its GUI image (file, index) from EMaterialType.
    /// </summary>
    static readonly (Material Material, int File, int Gui)[] StockGoods =
    {
        (Material.Plank, 3, 168), (Material.Stone, 3, 174), (Material.Trunk, 3, 171),
        (Material.Coal, 3, 144), (Material.IronOre, 3, 147), (Material.GoldOre, 3, 150),
        (Material.Iron, 3, 132), (Material.Hammer, 3, 126), (Material.Blade, 3, 138),
        (Material.Axe, 3, 153), (Material.Saw, 3, 177), (Material.Pick, 3, 129),
        (Material.FishingRod, 3, 141), (Material.Scythe, 3, 165), (Material.Sword, 14, 111),
        (Material.Bow, 14, 114), (Material.Spear, 14, 117), (Material.Mead, 14, 126),
        (Material.Fish, 3, 189), (Material.Pig, 3, 159), (Material.Meat, 3, 162),
        (Material.Crop, 3, 180), (Material.Flour, 3, 183), (Material.Bread, 3, 186),
        (Material.Water, 3, 156), (Material.Wine, 14, 123), (Material.Keg, 24, 123),
        (Material.Liquor, 34, 132), (Material.Rice, 34, 129), (Material.Honey, 14, 129),
        (Material.Gold, 3, 135), (Material.Gems, 24, 120), (Material.Sulfur, 34, 126),
        (Material.GunPowder, 34, 135)
    };

    readonly GL gl;
    readonly TextureAtlas atlas;
    readonly SpriteRenderer sprites;

    TextureRegion panelBg = null!;
    TextureRegion minimapDeco = null!;
    readonly TextureRegion[] primaryTabs = new TextureRegion[6];
    readonly TextureRegion[] secondaryTabs = new TextureRegion[8];
    readonly TextureRegion[] goodsTabs = new TextureRegion[8];
    readonly TextureRegion[] settlerTabs = new TextureRegion[8];
    readonly TextureRegion[] systemButton = new TextureRegion[2];
    readonly TextureRegion[] lowerTabs = new TextureRegion[8];
    readonly TextureRegion[] labeledButton = new TextureRegion[2];
    TextureRegion activeMark = null!;
    TextureRegion whiteRegion = null!;
    readonly Dictionary<BuildingType, TextureRegion> buildingIcons = new();
    readonly Dictionary<Material, TextureRegion> materialIcons = new();

    int primaryTab;      // 0 = build, 1 = goods, 2 = settlers
    int secondaryTab;    // 0 = normal, 1 = food, 2 = military, 3 = social
    BuildingType? selectedBuilding;
    bool systemMenuVisible;
    bool isPaused;

    /// <summary>The mouse position in screen pixels (for tooltips).</summary>
    public Vector2? MousePosition { get; set; }

    /// <summary>The building the mouse currently hovers over, if any.</summary>
    public BuildingType? HoveredBuilding { get; private set; }

    /// <summary>The player whose data the panel shows (inventory, population).</summary>
    public Player? Player { get; set; }

    /// <summary>The game, used to read the settler statistics.</summary>
    public Game? Game { get; set; }

    /// <summary>
    /// The building currently selected on the map. When set, the content
    /// area shows its info and status instead of the tab content.
    /// </summary>
    public Building? SelectedMapBuilding { get; set; }

    float scale = 1f;

    readonly List<ClickRegion> clickRegions = new();

    /// <summary>Raised when the player picks a building to construct.</summary>
    public event Action<BuildingType>? BuildingSelected;

    /// <summary>Raised when the system (computer) button is clicked.</summary>
    public event Action? SystemButtonClicked;

    /// <summary>Raised when the Pause button of the in-game menu is clicked.</summary>
    public event Action? PauseRequested;

    /// <summary>Raised when the Music button of the in-game menu is clicked.</summary>
    public event Action? MusicRequested;

    /// <summary>Raised when the Save button of the in-game menu is clicked.</summary>
    public event Action? SaveRequested;

    /// <summary>Raised when the Exit button of the in-game menu is clicked.</summary>
    public event Action? ExitRequested;

    /// <summary>Raised when the Cancel button of the in-game menu is clicked.</summary>
    public event Action? CancelRequested;

    /// <summary>Raised when the game speed should be increased.</summary>
    public event Action? SpeedUpRequested;

    /// <summary>Raised when the game speed should be decreased.</summary>
    public event Action? SpeedDownRequested;

    /// <summary>The current game speed (1..5), shown in the menu.</summary>
    public int GameSpeed { get; set; } = 1;

    public ControlPanel(GL gl)
    {
        this.gl = gl;
        atlas = new TextureAtlas(gl);
        sprites = new SpriteRenderer(gl);
    }

    /// <summary>Loads all panel sprites from the game data.</summary>
    public void Load(GameData gameData)
    {
        var buttons = gameData.LoadGfxFile(ButtonsFile);
        var panel = gameData.LoadGfxFile(PanelFile);
        var icons = gameData.LoadGfxFile(BuildingIconsFile);

        if (buttons == null || panel == null || icons == null)
            throw new InvalidOperationException("Control panel dat files (3, 4, 14) not found.");

        // Panel background and minimap decoration (768p frame).
        var bgFrames = panel.GetSettlerSequence(PanelBackgroundSequence);
        panelBg = AddImage(bgFrames[HighResFrame]);
        var decoFrames = panel.GetSettlerSequence(MiniMapDecorationSequence);
        minimapDeco = AddImage(decoFrames[HighResFrame]);

        // Primary tabs: build, goods, settlers (inactive + active).
        AddButtonPair(primaryTabs, 0, TabBuildInactive, TabBuildActive, buttons);
        AddButtonPair(primaryTabs, 2, TabGoodsInactive, TabGoodsActive, buttons);
        AddButtonPair(primaryTabs, 4, TabSettlersInactive, TabSettlersActive, buttons);

        // Secondary tabs: normal, food, military, social.
        AddButtonPair(secondaryTabs, 0, SubNormalInactive, SubNormalActive, buttons);
        AddButtonPair(secondaryTabs, 2, SubFoodInactive, SubFoodActive, buttons);
        AddButtonPair(secondaryTabs, 4, SubMilitaryInactive, SubMilitaryActive, buttons);
        AddButtonPair(secondaryTabs, 6, SubSocialInactive, SubSocialActive, buttons);

        // Goods secondary tabs: stock, tools, spread, transport.
        AddButtonPair(goodsTabs, 0, SubStockInactive, SubStockActive, buttons);
        AddButtonPair(goodsTabs, 2, SubToolsInactive, SubToolsActive, buttons);
        AddButtonPair(goodsTabs, 4, SubSpreadInactive, SubSpreadActive, buttons);
        AddButtonPair(goodsTabs, 6, SubTransportInactive, SubTransportActive, buttons);

        // Settlers secondary tabs: statistic, profession, warriors, production.
        AddButtonPair(settlerTabs, 0, SubStatisticInactive, SubStatisticActive, buttons);
        AddButtonPair(settlerTabs, 2, SubProfessionInactive, SubProfessionActive, buttons);
        AddButtonPair(settlerTabs, 4, SubWarriorsInactive, SubWarriorsActive, buttons);
        AddButtonPair(settlerTabs, 6, SubProductionInactive, SubProductionActive, buttons);

        // System (computer) button and lower tab bar.
        AddButtonPair(systemButton, 0, SystemInactive, SystemActive, buttons);
        AddButtonPair(lowerTabs, 0, LowerScrollInactive, LowerScrollActive, buttons);
        AddButtonPair(lowerTabs, 2, LowerSwordsInactive, LowerSwordsActive, buttons);
        AddButtonPair(lowerTabs, 4, LowerSignpostInactive, LowerSignpostActive, buttons);
        AddButtonPair(lowerTabs, 6, LowerPotsInactive, LowerPotsActive, buttons);

        // Labeled buttons of the in-game menu (settlers-remake LabeledButton).
        AddButtonPair(labeledButton, 0, LabeledButtonInactive, LabeledButtonActive, buttons);

        activeMark = AddImage(buttons.GetGui(ActiveMark)!);

        // 1x1 white pixel used to draw menu text as tinted quads.
        whiteRegion = AddImage(new DatImage(1, 1, 0, 0, new byte[] { 255, 255, 255, 255 }));

        // Building icons.
        foreach (var (type, gui) in NormalBuildings.Concat(FoodBuildings)
                     .Concat(MilitaryBuildings).Concat(SocialBuildings))
        {
            var image = icons.GetGui(gui);
            if (image != null)
                buildingIcons[type] = AddImage(image);
        }

        // Material icons for the stock overview (dat files 3, 14, 24, 34).
        var materialFiles = new Dictionary<int, DatFileReader>
        {
            [3] = buttons,
            [14] = gameData.LoadGfxFile(MaterialIconsFile2)!,
            [24] = gameData.LoadGfxFile(MaterialIconsFile3)!,
            [34] = gameData.LoadGfxFile(MaterialIconsFile4)!
        };
        foreach (var (material, file, gui) in StockGoods)
        {
            if (materialFiles.TryGetValue(file, out var gfx) && gfx.GetGui(gui) is DatImage image)
                materialIcons[material] = AddImage(image);
        }
    }

    TextureRegion AddImage(DatImage image) => atlas.AddImage(image.Rgba, image.Width, image.Height);

    void AddButtonPair(TextureRegion[] target, int offset, int inactive, int active, DatFileReader file)
    {
        target[offset] = AddImage(file.GetGui(inactive)!);
        target[offset + 1] = AddImage(file.GetGui(active)!);
    }

    /// <summary>The currently selected building, if any.</summary>
    public BuildingType? SelectedBuilding => selectedBuilding;

    /// <summary>Clears the building selection.</summary>
    public void ClearSelection()
    {
        selectedBuilding = null;
        SelectedMapBuilding = null;
    }

    /// <summary>Whether the in-game menu is shown in the content area.</summary>
    public bool SystemMenuVisible => systemMenuVisible;

    /// <summary>Whether the game is paused (drives the Pause button state).</summary>
    public bool IsPaused
    {
        get => isPaused;
        set => isPaused = value;
    }

    /// <summary>Closes the in-game menu without raising an event.</summary>
    public void CloseSystemMenu()
    {
        systemMenuVisible = false;
    }

    /// <summary>The screen rectangle of the minimap (for the minimap renderer).</summary>
    public (float X, float Y, float Width, float Height) GetMiniMapRect(int screenWidth, int screenHeight)
    {
        UpdateScale(screenHeight);
        // The minimap is a square centered in the decoration frame, sized
        // like the original game (fills most of the 216x224 frame).
        const float mapSize = 200f;
        float x = (PanelWidth - mapSize) / 2f;
        float y = (MiniMapHeight - mapSize) / 2f;
        return (x * scale, y * scale, mapSize * scale, mapSize * scale);
    }

    void UpdateScale(int screenHeight)
    {
        scale = screenHeight / TotalHeight;
    }

    /// <summary>Draws the control panel on the left edge of the screen.</summary>
    public void Draw(int screenWidth, int screenHeight)
    {
        UpdateScale(screenHeight);
        clickRegions.Clear();

        sprites.Begin();

        // Minimap decoration on top, panel background below it.
        AddSprite(minimapDeco, 0, 0, PanelWidth, MiniMapHeight);
        AddSprite(panelBg, 0, MiniMapHeight, PanelWidth, PanelHeight);

        // Primary tabs.
        for (int i = 0; i < 3; i++)
        {
            int tab = i;
            float x = PrimaryTabLeft + i * (PrimaryTabWidth + PrimaryTabSpacing);
            float y = MiniMapHeight + 9f;
            bool active = i == primaryTab;
            AddSprite(primaryTabs[i * 2 + (active ? 1 : 0)], x, y, PrimaryTabWidth, PrimaryTabHeight);
            if (!systemMenuVisible)
                AddClickRegion(x, y, PrimaryTabWidth, PrimaryTabHeight, () => SetPrimaryTab(tab));
        }

        if (SelectedMapBuilding != null)
        {
            // A building on the map is selected: show its info and status.
            DrawBuildingInfo();
        }
        else if (systemMenuVisible)
        {
            // The in-game menu replaces the building selection in the content
            // area (settlers-remake gamePanel layout).
            DrawSystemMenu();
        }
        else if (primaryTab == 1)
        {
            // Goods tab: stock, tools, spread, transport.
            DrawSecondaryTabs(goodsTabs, () => DrawGoodsContent());
        }
        else if (primaryTab == 2)
        {
            // Settlers tab: statistic, profession, warriors, production.
            DrawSecondaryTabs(settlerTabs, () => DrawSettlersContent());
        }
        else
        {
            // Build tab: normal, food, military, social building buttons.
            DrawSecondaryTabs(secondaryTabs, () => DrawBuildContent());
        }

        // System (computer) button toggles the in-game menu.
        AddSprite(systemButton[systemMenuVisible ? 1 : 0], SystemLeft, MiniMapHeight + SystemTop, SystemWidth, SystemHeight);
        AddClickRegion(SystemLeft, MiniMapHeight + SystemTop, SystemWidth, SystemHeight, ToggleSystemMenu);

        // Lower tab bar (scroll, swords, signpost, pots).
        for (int i = 0; i < 4; i++)
        {
            float x = LowerTabLeft + i * LowerTabSize;
            AddSprite(lowerTabs[i * 2], x, MiniMapHeight + LowerTabTop, LowerTabSize, LowerTabSize);
        }

        var projection = Matrix4x4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1, 1);
        atlas.Texture.Bind(0);
        sprites.Draw(projection, 1f);
    }

    (BuildingType Type, int Gui)[] CurrentBuildings() => secondaryTab switch
    {
        1 => FoodBuildings,
        2 => MilitaryBuildings,
        3 => SocialBuildings,
        _ => NormalBuildings
    };

    /// <summary>
    /// Draws the four secondary tabs of the current primary tab and the
    /// content below them.
    /// </summary>
    void DrawSecondaryTabs(TextureRegion[] tabs, Action drawContent)
    {
        for (int i = 0; i < 4; i++)
        {
            int tab = i;
            float x = SecondaryTabLeft + i * (SecondaryTabWidth + SecondaryTabSpacing);
            float y = MiniMapHeight + 64f;
            bool active = i == secondaryTab;
            AddSprite(tabs[i * 2 + (active ? 1 : 0)], x, y, SecondaryTabWidth, SecondaryTabHeight);
            AddClickRegion(x, y, SecondaryTabWidth, SecondaryTabHeight, () => SetSecondaryTab(tab));
        }

        drawContent();
    }

    /// <summary>Draws the building buttons of the build tab.</summary>
    void DrawBuildContent()
    {
        var buildings = CurrentBuildings();
        float cellWidth = ContentWidth / ContentColumns;
        float cellHeight = ContentHeight / ContentRows;
        HoveredBuilding = null;
        for (int i = 0; i < buildings.Length; i++)
        {
            var (type, _) = buildings[i];
            int col = i % ContentColumns;
            int row = i / ContentColumns;
            float x = ContentLeft + col * cellWidth;
            float y = MiniMapHeight + ContentTop + row * cellHeight;

            if (buildingIcons.TryGetValue(type, out var icon))
                AddCentered(icon, x, y, cellWidth, cellHeight);

            if (type == selectedBuilding)
                AddCentered(activeMark, x, y, cellWidth, cellHeight);

            // Tooltip: remember the building under the mouse.
            if (MousePosition is Vector2 mouse
                && mouse.X >= x * scale && mouse.X <= (x + cellWidth) * scale
                && mouse.Y >= y * scale && mouse.Y <= (y + cellHeight) * scale)
            {
                HoveredBuilding = type;
            }

            AddClickRegion(x, y, cellWidth, cellHeight, () => SelectBuilding(type));
        }

        // Draw the tooltip last so it is on top of the buttons.
        if (HoveredBuilding is BuildingType hovered)
            DrawTooltip(hovered);
    }

    /// <summary>
    /// Draws the info and status of the selected building: name, state
    /// (construction progress or active), worker, production inputs and
    /// output, and soldiers for military buildings.
    /// </summary>
    void DrawBuildingInfo()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        var building = SelectedMapBuilding;
        if (building == null)
            return;

        // Close button (X) in the top-right corner of the content area.
        const float closeSize = 16f;
        float closeX = contentX + contentW - closeSize;
        float closeY = contentY;
        AddSprite(whiteRegion, closeX, closeY, closeSize, closeSize, new Vector4(0.25f, 0.25f, 0.25f, 0.9f));
        DrawText("X", closeX + 4f, closeY + 2f, 2f, 3f);
        AddClickRegion(closeX, closeY, closeSize, closeSize, () => SelectedMapBuilding = null);

        // Building name.
        string name = building.Type.ToString().ToUpperInvariant();
        DrawText(name, contentX + 8f, contentY + 6f, 3f, 4f);

        float y = contentY + 34f;
        const float lineH = 20f;
        const float glyphW = 2f, glyphH = 3f;

        if (building.State == BuildingState.ConstructionSite)
        {
            DrawText($"CONSTRUCTION {building.ConstructionProgress}%", contentX + 8f, y, glyphW, glyphH);
            y += lineH;
            foreach (var (material, count) in building.Definition.ConstructionCosts)
            {
                int delivered = building.DeliveredMaterials.Get(material);
                DrawText($"{material} {delivered}/{count}", contentX + 8f, y, glyphW, glyphH);
                y += lineH;
            }
        }
        else if (building.State == BuildingState.Active)
        {
            DrawText("ACTIVE", contentX + 8f, y, glyphW, glyphH);
            y += lineH;

            if (building.HasWorker)
                DrawText($"WORKER: {building.Worker}", contentX + 8f, y, glyphW, glyphH);
            else
                DrawText("NO WORKER", contentX + 8f, y, glyphW, glyphH);
            y += lineH;

            if (building.Definition.IsProducer)
            {
                if (building.Definition.Inputs.Length > 0)
                {
                    DrawText("INPUT:", contentX + 8f, y, glyphW, glyphH);
                    y += lineH;
                    foreach (var input in building.Definition.Inputs)
                    {
                        DrawText($"  {input} {building.Input.Get(input)}", contentX + 8f, y, glyphW, glyphH);
                        y += lineH;
                    }
                }

                if (building.Definition.Output is Material output)
                {
                    var produced = building.Type == BuildingType.Toolsmith ? building.ToolsmithOutput : output;
                    DrawText($"OUTPUT: {produced}", contentX + 8f, y, glyphW, glyphH);
                    y += lineH;
                }
            }

            if (building.Definition.IsMilitary)
            {
                DrawText($"SOLDIERS: {building.Soldiers.Count}/{building.Definition.SoldierCapacity}", contentX + 8f, y, glyphW, glyphH);
                y += lineH;
            }
        }
    }

    /// <summary>Draws the tooltip for the given building next to the mouse.</summary>
    void DrawTooltip(BuildingType type)
    {
        if (MousePosition is not Vector2 mouse)
            return;

        string name = type.ToString().ToUpperInvariant();
        const float glyphW = 2f, glyphH = 3f;
        float textW = name.Length * glyphW * 5f;
        float textH = glyphH * 7f;
        float pad = 4f;
        float boxW = textW + pad * 2f;
        float boxH = textH + pad * 2f;

        // Place the tooltip to the right of the mouse, flipped left if it
        // would leave the panel.
        float x = mouse.X / scale + 10f;
        float y = mouse.Y / scale - boxH / 2f;
        if (x + boxW > PanelWidth)
            x = mouse.X / scale - boxW - 10f;
        y = Math.Clamp(y, MiniMapHeight, MiniMapHeight + PanelHeight - boxH);

        // Dark background box with a light border.
        var dark = new Vector4(0.1f, 0.1f, 0.1f, 0.9f);
        var light = new Vector4(0.8f, 0.8f, 0.8f, 1f);
        AddSprite(whiteRegion, x, y, boxW, boxH, dark);
        AddSprite(whiteRegion, x, y, boxW, 1f, light);
        AddSprite(whiteRegion, x, y + boxH - 1f, boxW, 1f, light);
        AddSprite(whiteRegion, x, y, 1f, boxH, light);
        AddSprite(whiteRegion, x + boxW - 1f, y, 1f, boxH, light);

        DrawText(name, x + pad, y + pad, glyphW, glyphH);
    }

    /// <summary>Draws the goods tab content (stock, tools, spread, transport).</summary>
    void DrawGoodsContent()
    {
        switch (secondaryTab)
        {
            case 1: DrawToolsContent(); break;
            case 2: DrawSpreadContent(); break;
            case 3: DrawTransportContent(); break;
            default: DrawStockContent(); break;
        }
    }

    /// <summary>Draws the settlers tab content (statistic, profession, warriors, production).</summary>
    void DrawSettlersContent()
    {
        switch (secondaryTab)
        {
            case 1: DrawProfessionContent(); break;
            case 2: DrawWarriorsContent(); break;
            case 3: DrawProductionContent(); break;
            default: DrawStatisticContent(); break;
        }
    }

    void SetPrimaryTab(int tab)
    {
        primaryTab = tab;
        if (tab != 0)
            selectedBuilding = null;
    }

    void SetSecondaryTab(int tab)
    {
        secondaryTab = tab;
        selectedBuilding = null;
    }

    void SelectBuilding(BuildingType type)
    {
        selectedBuilding = type;
        BuildingSelected?.Invoke(type);
    }

    void ToggleSystemMenu()
    {
        systemMenuVisible = !systemMenuVisible;
        SystemButtonClicked?.Invoke();
    }

    /// <summary>
    /// Draws the in-game menu in the content area. The buttons follow the
    /// settlers-remake gamePanel layout: relative positions within the
    /// content pane (y grows up in the reference), labeled buttons with the
    /// standard button background and centered text.
    /// </summary>
    void DrawSystemMenu()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        // Speed control and pause in the top row (settlers-remake gamePanel
        // layout): speed down, pause, speed label, speed up.
        AddMenuButton("PAUSE", contentX, contentY, contentW, contentH, 0.90f, 1.00f, isPaused, () => PauseRequested?.Invoke(), 0.25f, 0.60f);
        AddMenuButton("MUSIC", contentX, contentY, contentW, contentH, 0.75f, 0.85f, false, () => MusicRequested?.Invoke(), 0.25f, 0.90f);
        AddMenuButton("SAVE", contentX, contentY, contentW, contentH, 0.34f, 0.44f, false, () => SaveRequested?.Invoke());
        AddMenuButton("EXIT", contentX, contentY, contentW, contentH, 0.22f, 0.32f, false, () => ExitRequested?.Invoke());
        AddMenuButton("CANCEL", contentX, contentY, contentW, contentH, 0.10f, 0.20f, false, () => CancelRequested?.Invoke());

        // Speed arrows and label in the top row.
        AddMenuButton("<", contentX, contentY, contentW, contentH, 0.90f, 1.00f, false, () => SpeedDownRequested?.Invoke(), 0.10f, 0.25f);
        AddMenuButton(">", contentX, contentY, contentW, contentH, 0.90f, 1.00f, false, () => SpeedUpRequested?.Invoke(), 0.90f, 1.00f);
        float labelX = contentX + contentW * 0.60f;
        float labelY = contentY + contentH * (1f - 1.00f);
        float labelW = contentW * 0.30f;
        float labelH = contentH * 0.10f;
        DrawLabelCentered($"{GameSpeed}x", labelX, labelY, labelW, labelH);
    }

    void AddMenuButton(string label, float contentX, float contentY, float contentW, float contentH, float bottom, float top, bool active, Action action, float left = 0.1f, float right = 0.9f)
    {
        // The reference uses bottom-up relative coordinates within the pane.
        float x = contentX + contentW * left;
        float y = contentY + contentH * (1f - top);
        float w = contentW * (right - left);
        float h = contentH * (top - bottom);

        AddSprite(labeledButton[active ? 1 : 0], x, y, w, h);
        AddClickRegion(x, y, w, h, action);
        DrawLabelCentered(label, x, y, w, h);
    }

    /// <summary>
    /// Draws the stock overview in the content area: the player's inventory
    /// as a 6x6 grid of material icons with the amount below each icon,
    /// following the settlers-remake MaterialInventoryLayout.
    /// </summary>
    void DrawStockContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        const int columns = 6, rows = 6;
        float cellW = contentW / columns;
        float cellH = contentH / rows;

        for (int i = 0; i < StockGoods.Length; i++)
        {
            var (material, _, _) = StockGoods[i];
            int col = i % columns;
            int row = i / columns;
            float x = contentX + col * cellW;
            float y = contentY + row * cellH;

            if (materialIcons.TryGetValue(material, out var icon))
                AddCentered(icon, x, y, cellW, cellH * 0.6f);

            int count = Player?.Inventory.Get(material) ?? 0;
            DrawText(count.ToString(), x + (cellW - count.ToString().Length * 3f * 5f) / 2f,
                y + cellH * 0.62f, 3f, 4f);
        }
    }

    /// <summary>
    /// Draws the settler statistics in the content area: the population
    /// overview (total, free, builders, carriers, collectors, resource
    /// workers, soldiers) as a simple list, following the settlers-remake
    /// SettlersStatisticsPanel idea.
    /// </summary>
    void DrawStatisticContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        var game = Game;
        var player = Player;
        int total = player?.Population ?? 0;
        int free = player?.FreeSettlers ?? 0;
        int builders = 0, carriers = 0, collectors = 0, resourceWorkers = 0, soldiers = 0;
        if (game != null)
        {
            foreach (var settler in game.Settlers)
            {
                if (settler.Player != (player?.Index ?? 0))
                    continue;
                switch (settler.Type)
                {
                    case SerfType.Builder: builders++; break;
                    case SerfType.Carrier: carriers++; break;
                    case SerfType.Collector: collectors++; break;
                    case SerfType.ResourceWorker: resourceWorkers++; break;
                    case SerfType.Soldier: soldiers++; break;
                }
            }
        }

        var rows = new (string Label, int Value)[]
        {
            ("TOTAL", total),
            ("FREE", free),
            ("BUILDER", builders),
            ("CARRIER", carriers),
            ("COLLECTOR", collectors),
            ("RESOURCE", resourceWorkers),
            ("SOLDIER", soldiers)
        };

        float rowH = contentH / (rows.Length + 1);
        DrawText("SETTLERS", contentX + 8f, contentY + 4f, 3f, 4f);
        for (int i = 0; i < rows.Length; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(rows[i].Label, contentX + 8f, y, 3f, 4f);
            DrawText(rows[i].Value.ToString(), contentX + contentW - 8f - rows[i].Value.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the tools overview (all tools with their amounts).</summary>
    void DrawToolsContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("TOOLS", contentX + 8f, contentY + 4f, 3f, 4f);
        var tools = new (Material Material, string Label)[]
        {
            (Material.Hammer, "HAMMER"), (Material.Blade, "BLADE"), (Material.Pick, "PICK"),
            (Material.Axe, "AXE"), (Material.Saw, "SAW"), (Material.Scythe, "SCYTHE"),
            (Material.FishingRod, "ROD")
        };
        float rowH = contentH / (tools.Length + 1);
        for (int i = 0; i < tools.Length; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(tools[i].Label, contentX + 8f, y, 3f, 4f);
            int count = Player?.Inventory.Get(tools[i].Material) ?? 0;
            DrawText(count.ToString(), contentX + contentW - 8f - count.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the goods spread overview (raw materials and food).</summary>
    void DrawSpreadContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("SPREAD", contentX + 8f, contentY + 4f, 3f, 4f);
        var goods = new (Material Material, string Label)[]
        {
            (Material.Plank, "PLANK"), (Material.Stone, "STONE"), (Material.Trunk, "TRUNK"),
            (Material.Coal, "COAL"), (Material.IronOre, "ORE"), (Material.GoldOre, "GOLDORE"),
            (Material.Iron, "IRON"), (Material.Gold, "GOLD"), (Material.Bread, "BREAD"),
            (Material.Fish, "FISH"), (Material.Meat, "MEAT"), (Material.Crop, "CROP")
        };
        float rowH = contentH / (goods.Length + 1);
        for (int i = 0; i < goods.Length; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(goods[i].Label, contentX + 8f, y, 3f, 4f);
            int count = Player?.Inventory.Get(goods[i].Material) ?? 0;
            DrawText(count.ToString(), contentX + contentW - 8f - count.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the transport overview (goods in transit).</summary>
    void DrawTransportContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("TRANSPORT", contentX + 8f, contentY + 4f, 3f, 4f);
        int carriers = 0, flags = 0, roads = 0;
        if (Game != null)
        {
            carriers = Game.Carriers.Count();
            foreach (var network in Game.Networks)
            {
                flags += network.Flags.Count;
                roads += network.Roads.Count;
            }
        }
        var rows = new (string Label, int Value)[]
        {
            ("CARRIERS", carriers),
            ("FLAGS", flags),
            ("ROADS", roads)
        };
        float rowH = contentH / (rows.Length + 1);
        for (int i = 0; i < rows.Length; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(rows[i].Label, contentX + 8f, y, 3f, 4f);
            DrawText(rows[i].Value.ToString(), contentX + contentW - 8f - rows[i].Value.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the profession overview (workers by profession).</summary>
    void DrawProfessionContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("PROFESSIONS", contentX + 8f, contentY + 4f, 3f, 4f);
        var professions = new Dictionary<WorkerType, int>();
        if (Game != null)
        {
            foreach (var building in Game.Buildings)
            {
                if (building.Player != (Player?.Index ?? 0))
                    continue;
                if (building.Worker == WorkerType.None)
                    continue;
                professions[building.Worker] = professions.GetValueOrDefault(building.Worker) + 1;
            }
        }
        var rows = professions.OrderByDescending(p => p.Value).Take(10).ToList();
        float rowH = contentH / (rows.Count + 1);
        for (int i = 0; i < rows.Count; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(rows[i].Key.ToString().ToUpperInvariant(), contentX + 8f, y, 3f, 4f);
            DrawText(rows[i].Value.ToString(), contentX + contentW - 8f - rows[i].Value.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the warriors overview (soldiers by weapon).</summary>
    void DrawWarriorsContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("WARRIORS", contentX + 8f, contentY + 4f, 3f, 4f);
        int soldiers = 0;
        if (Game != null)
            soldiers = Game.Settlers.Count(s => s.Player == (Player?.Index ?? 0) && s.Type == SerfType.Soldier);
        var rows = new (string Label, int Value)[]
        {
            ("SOLDIERS", soldiers),
            ("SWORDS", Player?.Inventory.Get(Material.Sword) ?? 0),
            ("BOWS", Player?.Inventory.Get(Material.Bow) ?? 0),
            ("SPEARS", Player?.Inventory.Get(Material.Spear) ?? 0)
        };
        float rowH = contentH / (rows.Length + 1);
        for (int i = 0; i < rows.Length; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(rows[i].Label, contentX + 8f, y, 3f, 4f);
            DrawText(rows[i].Value.ToString(), contentX + contentW - 8f - rows[i].Value.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    /// <summary>Draws the production overview (active producers and their output).</summary>
    void DrawProductionContent()
    {
        float contentX = ContentLeft;
        float contentY = MiniMapHeight + ContentTop;
        float contentW = ContentWidth;
        float contentH = ContentHeight;

        DrawText("PRODUCTION", contentX + 8f, contentY + 4f, 3f, 4f);
        var rows = new List<(string Label, int Value)>();
        if (Game != null)
        {
            foreach (var building in Game.Buildings)
            {
                if (building.Player != (Player?.Index ?? 0))
                    continue;
                if (building.State != BuildingState.Active)
                    continue;
                if (!building.Definition.IsProducer)
                    continue;
                rows.Add((building.Type.ToString().ToUpperInvariant(), building.Output.TotalCount));
            }
        }
        if (rows.Count == 0)
            rows.Add(("NONE", 0));
        float rowH = contentH / (rows.Count + 1);
        for (int i = 0; i < rows.Count; i++)
        {
            float y = contentY + (i + 1) * rowH;
            DrawText(rows[i].Label, contentX + 8f, y, 3f, 4f);
            DrawText(rows[i].Value.ToString(), contentX + contentW - 8f - rows[i].Value.ToString().Length * 3f * 5f, y, 3f, 4f);
        }
    }

    void DrawLabelCentered(string text, float x, float y, float width, float height)
    {
        // Render the label with the 5x7 bitmap font, centered on the button.
        const float glyphW = 3f, glyphH = 4f;
        float textW = text.Length * glyphW * 5f;
        float textH = glyphH * 7f;
        DrawText(text, x + (width - textW) / 2f, y + (height - textH) / 2f, glyphW, glyphH);
    }

    void DrawText(string text, float x, float y, float glyphW, float glyphH)
    {
        float cx = x;
        foreach (char c in text)
        {
            if (BitmapFont.Glyphs.TryGetValue(char.ToUpperInvariant(c), out var glyph))
            {
                for (int row = 0; row < 7; row++)
                {
                    byte bits = glyph[row];
                    for (int col = 0; col < 5; col++)
                    {
                        if ((bits & (1 << (4 - col))) != 0)
                            AddSprite(whiteRegion, cx + col * glyphW, y + row * glyphH, glyphW, glyphH);
                    }
                }
            }
            cx += glyphW * 5f;
        }
    }

    void AddSprite(TextureRegion region, float x, float y, float width, float height)
    {
        sprites.Add(new Sprite(region, x * scale, y * scale, width * scale, height * scale));
    }

    void AddSprite(TextureRegion region, float x, float y, float width, float height, Vector4 color)
    {
        sprites.Add(new Sprite(region, x * scale, y * scale, width * scale, height * scale, 0f, color));
    }

    void AddCentered(TextureRegion region, float x, float y, float cellWidth, float cellHeight)
    {
        // Scale the icon to fit ~85% of the cell while keeping its aspect.
        float maxW = cellWidth * 0.85f;
        float maxH = cellHeight * 0.85f;
        float s = MathF.Min(maxW / region.Width, maxH / region.Height);
        float w = region.Width * s;
        float h = region.Height * s;
        AddSprite(region, x + (cellWidth - w) / 2, y + (cellHeight - h) / 2, w, h);
    }

    void AddClickRegion(float x, float y, float width, float height, Action action)
    {
        clickRegions.Add(new ClickRegion(x * scale, y * scale, width * scale, height * scale, action));
    }

    /// <summary>
    /// Handles a click in screen coordinates. Returns true if the click
    /// was consumed by the panel.
    /// </summary>
    public bool HandleClick(Vector2 position)
    {
        foreach (var region in clickRegions)
        {
            if (position.X >= region.X && position.X <= region.X + region.Width
                && position.Y >= region.Y && position.Y <= region.Y + region.Height)
            {
                region.Action();
                return true;
            }
        }
        return false;
    }

    readonly struct ClickRegion
    {
        public ClickRegion(float x, float y, float width, float height, Action action)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Action = action;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public Action Action { get; }
    }

    public void Dispose()
    {
        sprites.Dispose();
        atlas.Dispose();
    }
}
