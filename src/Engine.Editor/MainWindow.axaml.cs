using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
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
    private TilemapData  _activeLayer = new() { LayerName = "floor", LayerIndex = 0 };
    private TilesetData? _activeTileset;
    private TileId       _selectedTile = TileId.Empty;
    private bool         _isPainting;

    // Drag tracking for Rect and Line tools.
    private Vector2Int   _dragStartTile;
    private KeyModifiers _pressModifiers;

    // Imported tilesets shown in the Assets panel.
    private readonly ObservableCollection<TilesetData>  _importedTilesets = new();
    private readonly Dictionary<TilesetData, Bitmap>    _tilesetBitmaps   = new();

    public MainWindow()
    {
        InitializeComponent();

        // Seed the scene with one default tilemap layer.
        _scene.Tilemaps.Add(_activeLayer);

        // Scene name binding.
        SceneNameBox.Text = _scene.Name;
        SceneNameBox.TextChanged += OnSceneNameChanged;

        // File menu.
        MenuNewScene.Click      += OnNewScene;
        MenuOpenScene.Click     += OnOpenScene;
        MenuSaveScene.Click     += OnSaveScene;
        MenuImportTileset.Click += OnImportTilesetMenu;
        MenuExit.Click          += (_, _) => Close();

        // Assets panel.
        AssetsList.ItemsSource      = _importedTilesets;
        AssetsList.SelectionChanged += OnAssetSelected;

        // Tilemap tool panel.
        TilemapPanel.SetLayers([_activeLayer]);
        TilemapPanel.TilesetLoadRequested += OnTilesetLoadRequested;
        TilemapPanel.TileSelected += tile => _selectedTile = tile;
        TilemapPanel.ToolChanged  += _ => { };
        TilemapPanel.LayerChanged += layer => _activeLayer = layer ?? _activeLayer;
        TilemapPanel.LayerAdded   += OnLayerAdded;
        TilemapPanel.LayerRemoved += OnLayerRemoved;

        // Viewport painting events.
        MainViewport.ViewportPointerPressed  += OnViewportPointerPressed;
        MainViewport.ViewportPointerDragged  += OnViewportPointerDragged;
        MainViewport.ViewportPointerReleased += OnViewportPointerReleased;
    }

    // ── Tileset import ────────────────────────────────────────────────────────

    private async void OnImportTilesetMenu(object? sender, RoutedEventArgs e)
    {
        var files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Tileset PNG",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                    new FilePickerFileType("All Files") { Patterns = ["*"] },
                ],
            });

        if (files is not [var file]) return;
        ImportTileset(file.Path.LocalPath);
    }

    private void OnTilesetLoadRequested(string path) => ImportTileset(path);

    private void ImportTileset(string pngPath)
    {
        Bitmap avBitmap;
        try { avBitmap = new Bitmap(pngPath); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Editor] Failed to load tileset image: {ex.Message}");
            return;
        }

        string name   = Path.GetFileNameWithoutExtension(pngPath);
        int    imageW = avBitmap.PixelSize.Width;
        int    imageH = avBitmap.PixelSize.Height;

        // Load from sidecar if one exists; otherwise generate defaults.
        TilesetData tileset;
        string? sidecar = TilesetSerializer.FindSidecar(pngPath);
        if (sidecar is not null)
        {
            try
            {
                tileset = TilesetSerializer.Load(sidecar, name, pngPath, imageW, imageH);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Editor] Failed to load tileset sidecar: {ex.Message}");
                tileset = TilesetSerializer.Generate(name, pngPath, imageW, imageH, new Vector2Int(16, 16));
            }
        }
        else
        {
            tileset = TilesetSerializer.Generate(name, pngPath, imageW, imageH, new Vector2Int(16, 16));
        }

        // Save canonical tileset JSON to <project>/Assets/Tilesets/.
        try
        {
            string projectDir = _currentPath is not null
                ? Path.GetDirectoryName(_currentPath)!
                : Path.GetDirectoryName(pngPath)!;
            string tilesetDir = Path.Combine(projectDir, "Assets", "Tilesets");
            Directory.CreateDirectory(tilesetDir);
            string savePath  = Path.Combine(tilesetDir, name + ".tileset.json");
            int    imageCols = System.Math.Max(1, imageW / tileset.TileSize.X);
            TilesetSerializer.Save(tileset, savePath, imageCols);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Editor] Failed to save tileset JSON: {ex.Message}");
        }

        // Register in the Assets panel, replacing any previous entry with the same name.
        TilesetData? existing = null;
        foreach (var t in _importedTilesets)
            if (t.Name == tileset.Name) { existing = t; break; }

        if (existing is not null)
        {
            int idx = _importedTilesets.IndexOf(existing);
            _importedTilesets[idx] = tileset;
            _tilesetBitmaps.Remove(existing);
        }
        else
        {
            _importedTilesets.Add(tileset);
        }
        _tilesetBitmaps[tileset] = avBitmap;

        ActivateTileset(tileset, avBitmap);
    }

    private void ActivateTileset(TilesetData tileset, Bitmap avBitmap)
    {
        _activeTileset = tileset;
        if (!ReferenceEquals(AssetsList.SelectedItem, tileset))
            AssetsList.SelectedItem = tileset;
        TilemapPanel.SetPaletteContent(avBitmap, tileset);
        MainViewport.LoadTilemapLayer(_activeLayer, tileset, tileset.ImagePath);
    }

    private void OnAssetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (AssetsList.SelectedItem is TilesetData tileset &&
            !ReferenceEquals(tileset, _activeTileset) &&
            _tilesetBitmaps.TryGetValue(tileset, out var bm))
        {
            ActivateTileset(tileset, bm);
        }
    }

    // ── Layer management ──────────────────────────────────────────────────────

    private void OnLayerAdded(TilemapData layer)
    {
        _scene.Tilemaps.Add(layer);
        _activeLayer = layer;
    }

    private void OnLayerRemoved(TilemapData layer)
    {
        _scene.Tilemaps.Remove(layer);
        _activeLayer = TilemapPanel.ActiveLayer ?? _activeLayer;
    }

    // ── Viewport painting ─────────────────────────────────────────────────────

    private void OnViewportPointerPressed(Avalonia.Point pt, KeyModifiers modifiers)
    {
        _pressModifiers = modifiers;
        _dragStartTile  = PixelToTile(pt);
        _isPainting     = true;

        switch (TilemapPanel.ActiveTool)
        {
            case EditorTool.Paint:
                ApplyPaint(_dragStartTile);
                break;
            case EditorTool.Erase:
                ApplyErase(_dragStartTile);
                break;
            case EditorTool.Fill:
                FloodFill(_dragStartTile, _selectedTile, (_pressModifiers & KeyModifiers.Shift) != 0);
                _isPainting = false;
                break;
            case EditorTool.Picker:
                ApplyPicker(_dragStartTile);
                _isPainting = false;
                break;
            // Rect and Line: preview during drag, apply on release.
        }
    }

    private void OnViewportPointerDragged(Avalonia.Point pt)
    {
        if (!_isPainting) return;
        var tile = PixelToTile(pt);

        switch (TilemapPanel.ActiveTool)
        {
            case EditorTool.Paint:
                ApplyPaint(tile);
                break;
            case EditorTool.Erase:
                ApplyErase(tile);
                break;
            case EditorTool.Rect:
                if (_activeTileset is not null)
                    MainViewport.UpdateRectPreview(
                        _dragStartTile.X, _dragStartTile.Y,
                        tile.X, tile.Y,
                        _activeTileset.TileSize.X, _activeTileset.TileSize.Y);
                break;
            case EditorTool.Line:
                if (_activeTileset is not null)
                    MainViewport.UpdateLinePreview(TileCenter(_dragStartTile), TileCenter(tile));
                break;
        }
    }

    private void OnViewportPointerReleased(Avalonia.Point pt)
    {
        if (!_isPainting) { _isPainting = false; return; }
        _isPainting = false;

        var tile = PixelToTile(pt);
        switch (TilemapPanel.ActiveTool)
        {
            case EditorTool.Rect:
                ApplyRect(_dragStartTile, tile);
                MainViewport.ClearPreview();
                break;
            case EditorTool.Line:
                ApplyLine(_dragStartTile, tile);
                MainViewport.ClearPreview();
                break;
        }
    }

    // ── Tool implementations ──────────────────────────────────────────────────

    private void ApplyPaint(Vector2Int tile)
    {
        if (_selectedTile != TileId.Empty)
            _activeLayer.SetTile(tile, _selectedTile);
    }

    private void ApplyErase(Vector2Int tile)
    {
        _activeLayer.SetTile(tile, TileId.Empty);
    }

    private void ApplyPicker(Vector2Int tile)
    {
        var id = _activeLayer.GetTile(tile);
        if (id == TileId.Empty) return;
        TilemapPanel.SelectTileById(id);
        TilemapPanel.SetActiveTool(EditorTool.Paint);
    }

    private void ApplyRect(Vector2Int a, Vector2Int b)
    {
        if (_selectedTile == TileId.Empty) return;
        int x1 = System.Math.Min(a.X, b.X), x2 = System.Math.Max(a.X, b.X);
        int y1 = System.Math.Min(a.Y, b.Y), y2 = System.Math.Max(a.Y, b.Y);

        using var bulk = _activeLayer.BeginBulkEdit();
        for (int y = y1; y <= y2; y++)
        for (int x = x1; x <= x2; x++)
            _activeLayer.SetTile(new Vector2Int(x, y), _selectedTile);
    }

    private void ApplyLine(Vector2Int a, Vector2Int b)
    {
        if (_selectedTile == TileId.Empty) return;

        using var bulk = _activeLayer.BeginBulkEdit();
        foreach (var pos in BresenhamLine(a.X, a.Y, b.X, b.Y))
            _activeLayer.SetTile(pos, _selectedTile);
    }

    private void FloodFill(Vector2Int start, TileId paint, bool exactMatch)
    {
        if (_activeTileset is null || paint == TileId.Empty) return;

        var targetId = _activeLayer.GetTile(start);
        if (targetId == paint) return;

        string targetTag = (!exactMatch && targetId != TileId.Empty)
            ? GetTerrainTag(targetId) : "";

        bool Matches(TileId t) => exactMatch
            ? t == targetId
            : targetId == TileId.Empty ? t == TileId.Empty : GetTerrainTag(t) == targetTag;

        var queue   = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        queue.Enqueue(start);
        visited.Add(start);

        const int MaxTiles = 250_000;
        using var bulk = _activeLayer.BeginBulkEdit();

        while (queue.Count > 0 && visited.Count < MaxTiles)
        {
            var pos = queue.Dequeue();
            _activeLayer.SetTile(pos, paint);

            var up    = new Vector2Int(pos.X,     pos.Y - 1);
            var down  = new Vector2Int(pos.X,     pos.Y + 1);
            var left  = new Vector2Int(pos.X - 1, pos.Y);
            var right = new Vector2Int(pos.X + 1, pos.Y);

            foreach (var n in new[] { up, down, left, right })
            {
                if (visited.Add(n) && Matches(_activeLayer.GetTile(n)))
                    queue.Enqueue(n);
            }
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private Vector2Int PixelToTile(Avalonia.Point pt)
    {
        if (_activeTileset is null) return default;
        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) return default;
        return new Vector2Int((int)(pt.X / tileW), (int)(pt.Y / tileH));
    }

    private Avalonia.Point TileCenter(Vector2Int tile)
    {
        if (_activeTileset is null) return new(tile.X, tile.Y);
        return new(
            tile.X * _activeTileset.TileSize.X + _activeTileset.TileSize.X * 0.5,
            tile.Y * _activeTileset.TileSize.Y + _activeTileset.TileSize.Y * 0.5);
    }

    private string GetTerrainTag(TileId id)
    {
        if (id == TileId.Empty || _activeTileset is null) return "";
        foreach (var t in _activeTileset.Tiles)
            if (t.Id == id) return t.TerrainTag;
        return "";
    }

    private static IEnumerable<Vector2Int> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2;

        while (true)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 <  dy) { err += dx; y0 += sy; }
        }
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
        _currentPath   = null;
        _activeTileset = null;
        _importedTilesets.Clear();
        _tilesetBitmaps.Clear();
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
