using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Tiles;
using Engine.Editor.Tilemap;

namespace Engine.Editor;

/// <summary>The main editor window: File menu, four-panel layout, and SceneSerializer integration.</summary>
public partial class MainWindow : Window
{
    private SceneData _scene = new();
    private string? _currentPath;

    // Tilemap editing state.
    private TilemapData _activeLayer = new() { LayerName = "floor", LayerIndex = 0 };
    private TilesetData? _activeTileset;
    private TileId _selectedTile = TileId.Empty;
    private bool _isPainting;

    public MainWindow()
    {
        InitializeComponent();

        // Seed the scene with one default tilemap layer.
        _scene.Tilemaps.Add(_activeLayer);

        // Scene name binding.
        SceneNameBox.Text = _scene.Name;
        SceneNameBox.TextChanged += OnSceneNameChanged;

        // File menu.
        MenuNewScene.Click  += OnNewScene;
        MenuOpenScene.Click += OnOpenScene;
        MenuSaveScene.Click += OnSaveScene;
        MenuExit.Click      += (_, _) => Close();

        // Tilemap tool panel.
        TilemapPanel.SetLayers([_activeLayer]);
        TilemapPanel.TilesetLoadRequested += OnTilesetLoadRequested;
        TilemapPanel.TileSelected += tile => _selectedTile = tile;
        TilemapPanel.ToolChanged  += _ => { };  // ActiveTool read on demand
        TilemapPanel.LayerChanged += layer => _activeLayer = layer ?? _activeLayer;

        // Viewport painting events.
        MainViewport.ViewportPointerPressed  += OnViewportPointerPressed;
        MainViewport.ViewportPointerDragged  += OnViewportPointerDragged;
        MainViewport.ViewportPointerReleased += () => _isPainting = false;
    }

    // ── Tileset loading ───────────────────────────────────────────────────────

    private void OnTilesetLoadRequested(string path)
    {
        Bitmap avBitmap;
        try
        {
            avBitmap = new Bitmap(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Editor] Failed to load tileset image: {ex.Message}");
            return;
        }

        int imageW = avBitmap.PixelSize.Width;
        int imageH = avBitmap.PixelSize.Height;
        int tileW  = 16;
        int tileH  = 16;

        var tileset = new TilesetData
        {
            Name      = Path.GetFileNameWithoutExtension(path),
            ImagePath = path,
            TileSize  = new Vector2Int(tileW, tileH),
        };

        int cols = System.Math.Max(1, imageW / tileW);
        int rows = System.Math.Max(1, imageH / tileH);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            tileset.Tiles.Add(new TileMetadata
            {
                Id          = new TileId(r * cols + c + 1),
                BlobVariant = -1,
            });

        _activeTileset = tileset;
        TilemapPanel.SetPaletteContent(avBitmap, tileset);

        // Queue texture load + renderer setup inside the MonoGame tick loop.
        MainViewport.LoadTilemapLayer(_activeLayer, tileset, path);
    }

    // ── Viewport painting ─────────────────────────────────────────────────────

    private void OnViewportPointerPressed(Avalonia.Point pt)
    {
        _isPainting = true;
        ApplyTool(pt);
    }

    private void OnViewportPointerDragged(Avalonia.Point pt)
    {
        if (_isPainting) ApplyTool(pt);
    }

    private void ApplyTool(Avalonia.Point pt)
    {
        if (_activeTileset is null) return;

        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) return;

        int worldTileX = (int)(pt.X / tileW);
        int worldTileY = (int)(pt.Y / tileH);
        var worldPos = new Vector2Int(worldTileX, worldTileY);

        if (TilemapPanel.ActiveTool == EditorTool.Paint && _selectedTile != TileId.Empty)
            _activeLayer.SetTile(worldPos, _selectedTile);
        else if (TilemapPanel.ActiveTool == EditorTool.Erase)
            _activeLayer.SetTile(worldPos, TileId.Empty);
    }

    // ── Scene name ────────────────────────────────────────────────────────────

    private void OnSceneNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (SceneNameBox.Text is { } name)
            _scene.Name = name;
    }

    // ── File menu ─────────────────────────────────────────────────────────────

    private void OnNewScene(object? sender, RoutedEventArgs e)
    {
        _scene = new SceneData();
        _activeLayer = new TilemapData { LayerName = "floor", LayerIndex = 0 };
        _scene.Tilemaps.Add(_activeLayer);
        _currentPath = null;
        SceneNameBox.Text = _scene.Name;
        TilemapPanel.SetLayers([_activeLayer]);
        Title = "Tessera Engine Editor";
    }

    private async void OnOpenScene(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this)!;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Scene",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Tessera Scene") { Patterns = ["*.tscene"] },
                new FilePickerFileType("All Files")     { Patterns = ["*"] },
            ],
        });

        if (files is not [var file]) return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            _scene = SceneSerializer.Load(stream);
            _currentPath = file.Path.LocalPath;
            SceneNameBox.Text = _scene.Name;

            // Re-populate layer list from loaded scene.
            if (_scene.Tilemaps.Count == 0)
                _scene.Tilemaps.Add(new TilemapData { LayerName = "floor", LayerIndex = 0 });
            _activeLayer = _scene.Tilemaps[0];
            TilemapPanel.SetLayers(_scene.Tilemaps);

            Title = $"Tessera Engine Editor — {Path.GetFileName(_currentPath)}";
        }
        catch (SceneFormatException ex)
        {
            Console.Error.WriteLine($"[Editor] Failed to open scene: {ex.Message}");
        }
    }

    private async void OnSaveScene(object? sender, RoutedEventArgs e)
    {
        if (_currentPath is not null)
        {
            await using var fs = File.Create(_currentPath);
            SceneSerializer.Save(_scene, fs);
            return;
        }

        var topLevel = GetTopLevel(this)!;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Scene",
            DefaultExtension = "tscene",
            SuggestedFileName = _scene.Name,
            FileTypeChoices =
            [
                new FilePickerFileType("Tessera Scene") { Patterns = ["*.tscene"] },
            ],
        });

        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        SceneSerializer.Save(_scene, stream);
        _currentPath = file.Path.LocalPath;
        Title = $"Tessera Engine Editor — {Path.GetFileName(_currentPath)}";
    }
}
