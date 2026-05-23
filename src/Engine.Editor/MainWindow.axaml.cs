using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Engine.Core.Animation;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Sprites;
using Engine.Core.Tiles;
using Engine.Editor.Animation;
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

    // Tilesets.
    private readonly ObservableCollection<TilesetData>  _importedTilesets = new();
    private readonly Dictionary<TilesetData, Bitmap>    _tilesetBitmaps   = new();

    // Spritesheets + animation clips.
    private readonly List<SpritesheetBundle>       _importedSpritesheets = new();
    private SpritesheetBundle?                     _activeBundle;

    // Merged asset list shown in the Assets panel (TilesetData | SpritesheetBundle).
    private readonly ObservableCollection<object>  _allAssets = new();

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
        MenuLoadFixture.Click   += OnLoadTestFixture;
        MenuExit.Click          += (_, _) => Close();

        // Assets panel (shows tilesets and spritesheet bundles in one merged list).
        AssetsList.ItemsSource      = _allAssets;
        AssetsList.SelectionChanged += OnAssetSelected;

        // Tilemap tool panel.
        TilemapPanel.SetLayers([_activeLayer]);
        TilemapPanel.TilesetLoadRequested += OnTilesetLoadRequested;
        TilemapPanel.TileSelected += tile => { _selectedTile = tile; UpdateGhost(); };
        TilemapPanel.ToolChanged  += _    => UpdateGhost();
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

        RebuildAllAssets();
        ActivateTileset(tileset, avBitmap);
    }

    private void ActivateTileset(TilesetData tileset, Bitmap avBitmap)
    {
        _activeTileset = tileset;
        // SetPaletteContent resets the panel's SelectedTile but does NOT fire TileSelected,
        // so _selectedTile here would stay at the previous value and UpdateGhost() would try
        // to render the old tile ID against the new tileset's bitmap.  Reset explicitly.
        _selectedTile  = TileId.Empty;
        if (!ReferenceEquals(AssetsList.SelectedItem, tileset))
            AssetsList.SelectedItem = tileset;
        TilemapPanel.SetPaletteContent(avBitmap, tileset);
        MainViewport.LoadTilemapLayer(_activeLayer, tileset, tileset.ImagePath);
        MainViewport.SetTileSize(tileset.TileSize.X, tileset.TileSize.Y);
        UpdateGhost();
        ToolTabControl.SelectedIndex = 0;   // jump to Tilemap tab
    }

    private void ActivateSpritesheet(SpritesheetBundle bundle)
    {
        _activeBundle = bundle;
        if (!ReferenceEquals(AssetsList.SelectedItem, bundle))
            AssetsList.SelectedItem = bundle;
        AnimationPanel.SetContent(bundle.Sheet, bundle.Bitmap, bundle.Clips);
        ToolTabControl.SelectedIndex = 1;   // jump to Animation tab
    }

    private void OnAssetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (AssetsList.SelectedItem is TilesetData tileset &&
            !ReferenceEquals(tileset, _activeTileset) &&
            _tilesetBitmaps.TryGetValue(tileset, out var bm))
        {
            ActivateTileset(tileset, bm);
        }
        else if (AssetsList.SelectedItem is SpritesheetBundle bundle &&
                 !ReferenceEquals(bundle, _activeBundle))
        {
            ActivateSpritesheet(bundle);
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
        int   tileW = _activeTileset.TileSize.X;
        int   tileH = _activeTileset.TileSize.Y;
        int   zoom  = MainViewport.Zoom;
        float panX  = MainViewport.PanX;
        float panY  = MainViewport.PanY;
        if (tileW <= 0 || tileH <= 0 || zoom <= 0) return default;
        // screen pixel → world pixel → tile coordinate
        double worldX = pt.X / zoom + panX;
        double worldY = pt.Y / zoom + panY;
        return new Vector2Int(
            (int)System.Math.Floor(worldX / tileW),
            (int)System.Math.Floor(worldY / tileH));
    }

    private Avalonia.Point TileCenter(Vector2Int tile)
    {
        if (_activeTileset is null) return new(tile.X, tile.Y);
        int   tileW = _activeTileset.TileSize.X;
        int   tileH = _activeTileset.TileSize.Y;
        int   zoom  = MainViewport.Zoom;
        float panX  = MainViewport.PanX;
        float panY  = MainViewport.PanY;
        // world-pixel center of the tile, projected to screen coordinates
        double worldX = tile.X * tileW + tileW * 0.5;
        double worldY = tile.Y * tileH + tileH * 0.5;
        return new((worldX - panX) * zoom, (worldY - panY) * zoom);
    }

    /// <summary>
    /// Pushes the current ghost-tile state to the viewport.
    /// Shows a 50%-opacity preview of the selected tile in Paint mode; clears it otherwise.
    /// </summary>
    private void UpdateGhost()
    {
        if (TilemapPanel.ActiveTool != EditorTool.Paint ||
            _selectedTile == TileId.Empty               ||
            _activeTileset is null                      ||
            !_tilesetBitmaps.TryGetValue(_activeTileset, out var bm))
        {
            MainViewport.SetGhostTile(null, 0, 0, 0, 0);
            return;
        }

        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) { MainViewport.SetGhostTile(null, 0, 0, 0, 0); return; }

        int cols = bm.PixelSize.Width / tileW;

        // Use FindIndex — matches SelectTileById's formula exactly.  Do NOT use
        // Value-1 directly: sidecar-loaded tilesets may store tiles in non-sequential
        // order, making Value-1 give the wrong row for any tile past the first row.
        int idx = _activeTileset.Tiles.FindIndex(t => t.Id == _selectedTile);
        if (idx < 0) { MainViewport.SetGhostTile(null, 0, 0, 0, 0); return; }

        int srcX = (idx % cols) * tileW;
        int srcY = (idx / cols) * tileH;
        MainViewport.SetGhostTile(bm, srcX, srcY, tileW, tileH);
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
        _selectedTile  = TileId.Empty;
        _importedTilesets.Clear();
        _tilesetBitmaps.Clear();
        _importedSpritesheets.Clear();
        _activeBundle = null;
        _allAssets.Clear();
        AnimationPanel.SetContent(null, null, null);
        SceneNameBox.Text = _scene.Name;
        TilemapPanel.SetLayers([_activeLayer]);
        MainViewport.SetGhostTile(null, 0, 0, 0, 0);
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

    // ── Assets panel ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds <see cref="_allAssets"/> from the live tileset and spritesheet lists.
    /// Tilesets appear first (matching the historical order), followed by spritesheet bundles.
    /// </summary>
    private void RebuildAllAssets()
    {
        _allAssets.Clear();
        foreach (var t in _importedTilesets)     _allAssets.Add(t);
        foreach (var s in _importedSpritesheets) _allAssets.Add(s);
    }

    // ── Test fixture ──────────────────────────────────────────────────────────

    private void OnLoadTestFixture(object? sender, RoutedEventArgs e)
    {
        var bundle = CreateTestFixture();
        _importedSpritesheets.Add(bundle);
        RebuildAllAssets();
        ActivateSpritesheet(bundle);
    }

    /// <summary>
    /// Hand-constructs a <see cref="SpritesheetBundle"/> with 4 16×16 frames and two clips
    /// ("walk_south" — 4 frames × 100 ms, looping; "idle_south" — 1 frame × 500 ms, looping)
    /// plus a programmatic BGRA8888 bitmap with one distinct solid colour per frame.
    /// Used for manual verification of the Animation panel before Aseprite import (Task 2.6).
    /// </summary>
    private static SpritesheetBundle CreateTestFixture()
    {
        const int fw = 16, fh = 16, frameCount = 4;

        var sheet = SpritesheetData.GenerateGrid(
            name:      "test_fixture",
            imagePath: "",
            imageSize: new Engine.Core.Math.Vector2Int(fw * frameCount, fh),
            frameSize: new Engine.Core.Math.Vector2Int(fw, fh));

        var walkSouth = new AnimationClip
        {
            Name  = "walk_south",
            Loops = true,
            Frames =
            [
                new AnimationClipFrame { SpriteId = new SpriteId(1), DurationMs = 100 },
                new AnimationClipFrame { SpriteId = new SpriteId(2), DurationMs = 100 },
                new AnimationClipFrame { SpriteId = new SpriteId(3), DurationMs = 100 },
                new AnimationClipFrame { SpriteId = new SpriteId(4), DurationMs = 100 },
            ],
        };

        var idleSouth = new AnimationClip
        {
            Name  = "idle_south",
            Loops = true,
            Frames =
            [
                new AnimationClipFrame { SpriteId = new SpriteId(1), DurationMs = 500 },
            ],
        };

        var bitmap = CreateFixtureBitmap(fw, fh, frameCount);
        return new SpritesheetBundle(sheet, bitmap, [walkSouth, idleSouth]);
    }

    /// <summary>
    /// Creates a <see cref="WriteableBitmap"/> containing <paramref name="frameCount"/>
    /// solid-colour blocks each <paramref name="fw"/>×<paramref name="fh"/> pixels wide,
    /// laid out horizontally.  The colours cycle through red, green, blue, and yellow so
    /// each frame is visually distinct in the thumbnail strip.
    /// </summary>
    private static WriteableBitmap CreateFixtureBitmap(int fw, int fh, int frameCount)
    {
        // BGRA8888 layout: [blue, green, red, alpha] per pixel.
        ReadOnlySpan<(byte B, byte G, byte R)> palette = stackalloc (byte, byte, byte)[]
        {
            (0,   0,   255), // frame 0 — red
            (0,   200, 0  ), // frame 1 — green
            (220, 0,   0  ), // frame 2 — blue
            (0,   200, 200), // frame 3 — yellow
        };

        int totalW  = fw * frameCount;
        int stride  = totalW * 4;
        var pixels  = new byte[stride * fh];

        for (int fi = 0; fi < frameCount; fi++)
        {
            var (b, g, r) = palette[fi % palette.Length];
            for (int py = 0; py < fh; py++)
            for (int px = 0; px < fw; px++)
            {
                int off = (py * totalW + fi * fw + px) * 4;
                pixels[off    ] = b;
                pixels[off + 1] = g;
                pixels[off + 2] = r;
                pixels[off + 3] = 255;
            }
        }

        var wb = new WriteableBitmap(
            new PixelSize(totalW, fh),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var fb = wb.Lock();
        Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        return wb;
    }
}
