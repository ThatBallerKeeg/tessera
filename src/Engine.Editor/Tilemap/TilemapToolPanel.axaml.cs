using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Engine.Core.Tiles;
using Engine.Editor;

namespace Engine.Editor.Tilemap;

/// <summary>
/// Right-panel control housing tileset selector, tile palette, tool buttons, and layer selector.
/// </summary>
public partial class TilemapToolPanel : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks "..." and picks a PNG.</summary>
    public event Action<string>?       TilesetLoadRequested;

    /// <summary>Fired when the user clicks a tile in the palette.</summary>
    public event Action<TileId>?       TileSelected;

    /// <summary>Fired when the active editing tool changes.</summary>
    public event Action<EditorTool>?   ToolChanged;

    /// <summary>Fired when the layer dropdown selection changes.</summary>
    public event Action<TilemapData?>? LayerChanged;

    /// <summary>Fired after a new layer is created and added to the panel's list.</summary>
    public event Action<TilemapData>?  LayerAdded;

    /// <summary>Fired after a layer is removed from the panel's list.</summary>
    public event Action<TilemapData>?  LayerRemoved;

    // ── State ─────────────────────────────────────────────────────────────────

    public TileId       SelectedTile { get; private set; } = TileId.Empty;
    public EditorTool   ActiveTool   { get; private set; } = EditorTool.Paint;
    public TilemapData? ActiveLayer  { get; private set; }

    private readonly ObservableCollection<TilesetData> _tilesets = new();
    private readonly ObservableCollection<TilemapData> _layers   = new();

    /// <summary>
    /// Display scale applied to the tile palette. Each source pixel is rendered as a
    /// PaletteScale × PaletteScale block so tiles are readable at typical monitor sizes.
    /// Hit-detection divides display coords by this value; SelectionBorder positions and
    /// sizes are multiplied by it.
    /// </summary>
    private const int PaletteScale = 3;

    private TilesetData? _activeTileset;
    private Bitmap?      _paletteBitmap;

    // ── Construction ──────────────────────────────────────────────────────────

    public TilemapToolPanel()
    {
        InitializeComponent();

        TilesetSelector.ItemsSource = _tilesets;
        LayerSelector.ItemsSource   = _layers;

        LoadTilesetButton.Click          += OnLoadTilesetClicked;
        PaletteGrid.PointerPressed       += OnPalettePointerPressed;
        TilesetSelector.SelectionChanged += OnTilesetSelectionChanged;
        LayerSelector.SelectionChanged   += OnLayerSelectionChanged;

        NewLayerButton.Click    += OnNewLayerClicked;
        DeleteLayerButton.Click += OnDeleteLayerClicked;
        MoveUpButton.Click      += OnMoveUpClicked;
        MoveDownButton.Click    += OnMoveDownClicked;

        PaintToolButton.IsCheckedChanged  += (_, _) => { if (PaintToolButton.IsChecked  == true) SetTool(EditorTool.Paint);  };
        EraseToolButton.IsCheckedChanged  += (_, _) => { if (EraseToolButton.IsChecked  == true) SetTool(EditorTool.Erase);  };
        FillToolButton.IsCheckedChanged   += (_, _) => { if (FillToolButton.IsChecked   == true) SetTool(EditorTool.Fill);   };
        RectToolButton.IsCheckedChanged   += (_, _) => { if (RectToolButton.IsChecked   == true) SetTool(EditorTool.Rect);   };
        LineToolButton.IsCheckedChanged   += (_, _) => { if (LineToolButton.IsChecked   == true) SetTool(EditorTool.Line);   };
        PickerToolButton.IsCheckedChanged += (_, _) => { if (PickerToolButton.IsChecked == true) SetTool(EditorTool.Picker); };
    }

    // ── Public API called by MainWindow ───────────────────────────────────────

    /// <summary>Populates the layer dropdown and selects the first entry.</summary>
    public void SetLayers(IReadOnlyList<TilemapData> layers)
    {
        _layers.Clear();
        foreach (var l in layers) _layers.Add(l);
        if (_layers.Count > 0)
        {
            LayerSelector.SelectedIndex = 0;
            ActiveLayer = _layers[0];
        }
        UpdateLayerButtons();
    }

    /// <summary>
    /// Displays <paramref name="bitmap"/> as the tile palette for <paramref name="tileset"/>.
    /// The image is shown at <see cref="PaletteScale"/>× its natural pixel size so individual
    /// tiles are large enough to read and click.
    /// </summary>
    public void SetPaletteContent(Bitmap bitmap, TilesetData tileset)
    {
        if (!_tilesets.Contains(tileset))
            _tilesets.Add(tileset);
        TilesetSelector.SelectedItem = tileset;

        _paletteBitmap      = bitmap;
        _activeTileset      = tileset;
        PaletteImage.Source = bitmap;
        PaletteImage.Width  = bitmap.PixelSize.Width  * PaletteScale;
        PaletteImage.Height = bitmap.PixelSize.Height * PaletteScale;

        SelectionBorder.IsVisible = false;
        SelectedTileLabel.Text    = "Selected: —";
        SelectedTile = TileId.Empty;
    }

    /// <summary>Highlights the palette cell for <paramref name="id"/> and fires TileSelected.</summary>
    public void SelectTileById(TileId id)
    {
        if (_activeTileset is null || _paletteBitmap is null) return;
        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) return;

        int idx = _activeTileset.Tiles.FindIndex(t => t.Id == id);
        if (idx < 0) return;

        int cols  = _paletteBitmap.PixelSize.Width / tileW;
        int tileX = idx % cols;
        int tileY = idx / cols;

        SelectedTile = id;
        SelectedTileLabel.Text = $"Selected: {id.Value}";
        TileSelected?.Invoke(id);

        Avalonia.Controls.Canvas.SetLeft(SelectionBorder, tileX * tileW * PaletteScale);
        Avalonia.Controls.Canvas.SetTop(SelectionBorder,  tileY * tileH * PaletteScale);
        SelectionBorder.Width     = tileW * PaletteScale;
        SelectionBorder.Height    = tileH * PaletteScale;
        SelectionBorder.IsVisible = true;
    }

    /// <summary>Programmatically activates <paramref name="tool"/>.</summary>
    public void SetActiveTool(EditorTool tool) => SetTool(tool);

    // ── Layer management handlers ─────────────────────────────────────────────

    private async void OnNewLayerClicked(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        string defaultName = $"Layer {_layers.Count + 1}";
        string? name = await Dialogs.ShowNameInputAsync(owner, "New Layer", defaultName);
        if (name is null) return;

        int maxIndex = _layers.Count > 0 ? _layers.Max(l => l.LayerIndex) : -1;
        var layer    = new TilemapData { LayerName = name, LayerIndex = maxIndex + 1 };

        _layers.Add(layer);
        LayerSelector.SelectedItem = layer;
        UpdateLayerButtons();

        LayerAdded?.Invoke(layer);
    }

    private async void OnDeleteLayerClicked(object? sender, RoutedEventArgs e)
    {
        if (ActiveLayer is null || _layers.Count <= 1) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        bool confirmed = await Dialogs.ShowConfirmAsync(
            owner,
            $"Delete layer '{ActiveLayer.LayerName}'? This cannot be undone.",
            "Delete Layer");

        if (!confirmed) return;

        var removed = ActiveLayer;
        int idx     = _layers.IndexOf(removed);
        _layers.Remove(removed);

        // Select the adjacent layer that's still present.
        LayerSelector.SelectedIndex = Math.Min(idx, _layers.Count - 1);
        UpdateLayerButtons();

        LayerRemoved?.Invoke(removed);
    }

    private void OnMoveUpClicked(object? sender, RoutedEventArgs e)
    {
        if (ActiveLayer is null) return;
        int idx = _layers.IndexOf(ActiveLayer);
        if (idx <= 0) return;

        // Swap LayerIndex values between active and the one above it.
        int tmp = _layers[idx].LayerIndex;
        _layers[idx].LayerIndex     = _layers[idx - 1].LayerIndex;
        _layers[idx - 1].LayerIndex = tmp;

        _layers.Move(idx, idx - 1);
        LayerSelector.SelectedItem = ActiveLayer;
        UpdateLayerButtons();
    }

    private void OnMoveDownClicked(object? sender, RoutedEventArgs e)
    {
        if (ActiveLayer is null) return;
        int idx = _layers.IndexOf(ActiveLayer);
        if (idx < 0 || idx >= _layers.Count - 1) return;

        // Swap LayerIndex values between active and the one below it.
        int tmp = _layers[idx].LayerIndex;
        _layers[idx].LayerIndex     = _layers[idx + 1].LayerIndex;
        _layers[idx + 1].LayerIndex = tmp;

        _layers.Move(idx, idx + 1);
        LayerSelector.SelectedItem = ActiveLayer;
        UpdateLayerButtons();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void SetTool(EditorTool tool)
    {
        ActiveTool = tool;
        PaintToolButton.IsChecked  = tool == EditorTool.Paint;
        EraseToolButton.IsChecked  = tool == EditorTool.Erase;
        FillToolButton.IsChecked   = tool == EditorTool.Fill;
        RectToolButton.IsChecked   = tool == EditorTool.Rect;
        LineToolButton.IsChecked   = tool == EditorTool.Line;
        PickerToolButton.IsChecked = tool == EditorTool.Picker;
        ToolChanged?.Invoke(tool);
    }

    private void UpdateLayerButtons()
    {
        int idx = ActiveLayer is null ? -1 : _layers.IndexOf(ActiveLayer);
        DeleteLayerButton.IsEnabled = _layers.Count > 1;
        MoveUpButton.IsEnabled      = idx > 0;
        MoveDownButton.IsEnabled    = idx >= 0 && idx < _layers.Count - 1;
    }

    // ── Internal handlers ─────────────────────────────────────────────────────

    private async void OnLoadTilesetClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Tileset PNG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            ],
        });

        if (files is not [var file]) return;
        TilesetLoadRequested?.Invoke(file.Path.LocalPath);
    }

    private void OnPalettePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeTileset is null || _paletteBitmap is null) return;
        if (!e.GetCurrentPoint(PaletteGrid).Properties.IsLeftButtonPressed) return;

        // pos is in display space (PaletteScale× larger than source pixels).
        // Divide by PaletteScale to get the source-pixel coordinate for tile calculation.
        var pos   = e.GetPosition(PaletteImage);
        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) return;

        int tileX = (int)(pos.X / (tileW * PaletteScale));
        int tileY = (int)(pos.Y / (tileH * PaletteScale));
        int cols  = _paletteBitmap.PixelSize.Width / tileW;
        int id    = tileY * cols + tileX + 1;

        SelectedTile = new TileId(id);
        SelectedTileLabel.Text = $"Selected: {id}";
        TileSelected?.Invoke(SelectedTile);

        // Selection border is positioned in display space — multiply by PaletteScale.
        Avalonia.Controls.Canvas.SetLeft(SelectionBorder, tileX * tileW * PaletteScale);
        Avalonia.Controls.Canvas.SetTop(SelectionBorder,  tileY * tileH * PaletteScale);
        SelectionBorder.Width     = tileW * PaletteScale;
        SelectionBorder.Height    = tileH * PaletteScale;
        SelectionBorder.IsVisible = true;
    }

    private void OnTilesetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TilesetSelector.SelectedItem is TilesetData ts)
            _activeTileset = ts;
    }

    private void OnLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LayerSelector.SelectedItem is TilemapData layer)
        {
            ActiveLayer = layer;
            LayerChanged?.Invoke(layer);
            UpdateLayerButtons();
        }
    }
}
