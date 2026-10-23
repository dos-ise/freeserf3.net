/*
 * MainMenu.cs - Start menu shown before the game starts
 *
 * Lets the player pick a game mode (single player, multiplayer, options,
 * exit) and, for single player, a map to start on. The layout follows the
 * settlers-remake menu style: labeled buttons with centered text.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Freeserf3.Data;
using Freeserf3.Render;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Freeserf3.Renderer;

/// <summary>The screens of the start menu.</summary>
public enum MainMenuScreen
{
    /// <summary>The main menu with the game modes.</summary>
    Main,

    /// <summary>The single-player map selection.</summary>
    MapSelection
}

/// <summary>
/// The start menu: game mode selection and single-player map selection.
/// Drawn full-screen before the game loads.
/// </summary>
public sealed class MainMenu : IDisposable
{
    /// <summary>Labeled buttons are stored in dat file 3.</summary>
    public const int ButtonsFile = 3;

    const int LabeledButtonInactive = 324, LabeledButtonActive = 327;

    readonly GL gl;
    readonly TextureAtlas atlas;
    readonly SpriteRenderer sprites;

    TextureRegion[] labeledButton = new TextureRegion[2];
    TextureRegion whiteRegion = null!;

    readonly List<ClickRegion> clickRegions = new();

    /// <summary>The currently shown screen.</summary>
    public MainMenuScreen Screen { get; private set; } = MainMenuScreen.Main;

    /// <summary>The available single-player maps.</summary>
    public IReadOnlyList<string> Maps { get; private set; } = Array.Empty<string>();

    /// <summary>The index of the selected map in the map list.</summary>
    public int SelectedMap { get; private set; }

    /// <summary>Raised when the player picks the single-player mode.</summary>
    public event Action? SinglePlayerSelected;

    /// <summary>Raised when the player picks a map to start on.</summary>
    public event Action<string>? MapSelected;

    /// <summary>Raised when the player picks the multiplayer mode.</summary>
    public event Action? MultiplayerSelected;

    /// <summary>Raised when the player picks the options mode.</summary>
    public event Action? OptionsSelected;

    /// <summary>Raised when the player picks exit.</summary>
    public event Action? ExitRequested;

    public MainMenu(GL gl)
    {
        this.gl = gl;
        atlas = new TextureAtlas(gl);
        sprites = new SpriteRenderer(gl);
    }

    /// <summary>Loads the menu sprites and the map list.</summary>
    public void Load(GameData gameData)
    {
        var buttons = gameData.LoadGfxFile(ButtonsFile);
        if (buttons == null)
            throw new InvalidOperationException("Buttons dat file (3) not found.");

        labeledButton[0] = AddImage(buttons.GetGui(LabeledButtonInactive)!);
        labeledButton[1] = AddImage(buttons.GetGui(LabeledButtonActive)!);

        // 1x1 white pixel used to draw text as tinted quads.
        whiteRegion = AddImage(new DatImage(1, 1, 0, 0, new byte[] { 255, 255, 255, 255 }));

        Maps = gameData.FindSinglePlayerMapFiles().ToList();
    }

    TextureRegion AddImage(DatImage image) => atlas.AddImage(image.Rgba, image.Width, image.Height);

    /// <summary>Shows the map selection screen.</summary>
    public void ShowMapSelection()
    {
        Screen = MainMenuScreen.MapSelection;
        SelectedMap = 0;
    }

    /// <summary>Shows the main menu screen.</summary>
    public void ShowMain()
    {
        Screen = MainMenuScreen.Main;
    }

    /// <summary>Draws the menu full-screen.</summary>
    public void Draw(int screenWidth, int screenHeight)
    {
        clickRegions.Clear();
        sprites.Begin();

        if (Screen == MainMenuScreen.MapSelection)
            DrawMapSelection(screenWidth, screenHeight);
        else
            DrawMain(screenWidth, screenHeight);

        var projection = Matrix4x4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1, 1);
        atlas.Texture.Bind(0);
        sprites.Draw(projection, 1f);
    }

    void DrawMain(int screenWidth, int screenHeight)
    {
        // Title.
        DrawTextCentered("FREESERF3.NET", screenWidth / 2f, screenHeight * 0.12f, 6f, 8f);

        // Game mode buttons, centered vertically.
        var buttons = new (string Label, Action Action)[]
        {
            ("SINGLE PLAYER", () => SinglePlayerSelected?.Invoke()),
            ("MULTIPLAYER", () => MultiplayerSelected?.Invoke()),
            ("OPTIONS", () => OptionsSelected?.Invoke()),
            ("EXIT", () => ExitRequested?.Invoke())
        };

        float buttonW = 360f;
        float buttonH = 56f;
        float spacing = 20f;
        float totalH = buttons.Length * buttonH + (buttons.Length - 1) * spacing;
        float y = (screenHeight - totalH) / 2f;

        for (int i = 0; i < buttons.Length; i++)
        {
            float x = (screenWidth - buttonW) / 2f;
            AddButton(buttons[i].Label, x, y, buttonW, buttonH, buttons[i].Action);
            y += buttonH + spacing;
        }
    }

    void DrawMapSelection(int screenWidth, int screenHeight)
    {
        DrawTextCentered("SELECT MAP", screenWidth / 2f, screenHeight * 0.08f, 5f, 7f);

        // Map list (up to 8 visible).
        const int visible = 8;
        float listW = 520f;
        float listH = 44f;
        float spacing = 8f;
        float listX = (screenWidth - listW) / 2f;
        float listY = screenHeight * 0.16f;

        int start = Math.Max(0, SelectedMap - visible + 1);
        int end = Math.Min(Maps.Count, start + visible);
        for (int i = start; i < end; i++)
        {
            int index = i;
            string name = Path.GetFileNameWithoutExtension(Maps[i]);
            bool active = i == SelectedMap;
            AddButton(name, listX, listY, listW, listH, () => SelectMap(index), active);
            listY += listH + spacing;
        }

        // Start and back buttons.
        float bottomY = screenHeight * 0.85f;
        AddButton("START", screenWidth / 2f - 190f, bottomY, 180f, 50f, () => StartSelectedMap());
        AddButton("BACK", screenWidth / 2f + 10f, bottomY, 180f, 50f, () => ShowMain());
    }

    void SelectMap(int index)
    {
        SelectedMap = index;
    }

    void StartSelectedMap()
    {
        if (SelectedMap >= 0 && SelectedMap < Maps.Count)
            MapSelected?.Invoke(Maps[SelectedMap]);
    }

    void AddButton(string label, float x, float y, float width, float height, Action action, bool active = false)
    {
        AddSprite(labeledButton[active ? 1 : 0], x, y, width, height);
        clickRegions.Add(new ClickRegion(x, y, width, height, action));
        DrawLabelCentered(label, x, y, width, height);
    }

    void DrawLabelCentered(string text, float x, float y, float width, float height)
    {
        const float glyphW = 3f, glyphH = 4f;
        float textW = text.Length * glyphW * 5f;
        float textH = glyphH * 7f;
        DrawText(text, x + (width - textW) / 2f, y + (height - textH) / 2f, glyphW, glyphH);
    }

    void DrawTextCentered(string text, float centerX, float y, float glyphW, float glyphH)
    {
        float textW = text.Length * glyphW * 5f;
        DrawText(text, centerX - textW / 2f, y, glyphW, glyphH);
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
        sprites.Add(new Sprite(region, x, y, width, height));
    }

    /// <summary>
    /// Handles a click in screen coordinates. Returns true if the click
    /// was consumed by the menu.
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
