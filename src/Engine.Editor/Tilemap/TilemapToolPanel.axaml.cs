using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Engine.Core.Tiles;

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

    // ── State ─────────────────────────────────────────────────────────────────

    public TileId      SelectedTile { get; private set; } = TileId.Empty;
    public EditorTool  ActiveTool   { get; private set; } = EditorTool.Paint;
    public TilemapData? ActiveLayer  { get; private set; }

    private readonly ObservableCollection<TilesetData> _tilesets = new();
    private readonly ObservableCollection<TilemapData> _layers   = new();

    private TilesetData? _activeTileset;
    private Bitmap?      _paletteBitmap;

    // ── Construction ──────────────────────────────────────────────────────────

    public TilemapToolPanel()
    {
        InitializeComponent();

        TilesetSelector.ItemsSource = _tilesets;
        LayerSelector.ItemsSource   = _layers;

        LoadTilesetButton.Click      += OnLoadTilesetClicked;
        PaletteGrid.PointerPressed   += OnPalettePointerPressed;
        TilesetSelector.SelectionChanged += OnTilesetSelectionChanged;
        LayerSelector.SelectionChanged   += OnLayerSelectionChanged;

        PaintToolButton.IsCheckedChanged += (_, _) =>
        {
            if (PaintToolButton.IsChecked == true)
            {
                EraseToolButton.IsChecked = false;
                ActiveTool = EditorTool.Paint;
                ToolChanged?.Invoke(EditorTool.Paint);
            }
        };

        EraseToolButton.IsCheckedChanged += (_, _) =>
        {
            if (EraseToolButton.IsChecked == true)
            {
                PaintToolButton.IsChecked = false;
                ActiveTool = EditorTool.Erase;
                ToolChanged?.Invoke(EditorTool.Erase);
            }
        };
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
    }

    /// <summary>
    /// Displays <paramref name="bitmap"/> as the tile palette for <paramref name="tileset"/>.
    /// </summary>
    public void SetPaletteContent(Bitmap bitmap, TilesetData tileset)
    {
        if (!_tilesets.Contains(tileset))
            _tilesets.Add(tileset);
        TilesetSelector.SelectedItem = tileset;

        _paletteBitmap  = bitmap;
        _activeTileset  = tileset;
        PaletteImage.Source = bitmap;

        SelectionBorder.IsVisible = false;
        SelectedTileLabel.Text    = "Selected: —";
        SelectedTile = TileId.Empty;
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

        var pos  = e.GetPosition(PaletteImage);
        int tileW = _activeTileset.TileSize.X;
        int tileH = _activeTileset.TileSize.Y;
        if (tileW <= 0 || tileH <= 0) return;

        int tileX = (int)(pos.X / tileW);
        int tileY = (int)(pos.Y / tileH);
        int cols  = _paletteBitmap.PixelSize.Width / tileW;
        int id    = tileY * cols + tileX + 1;

        SelectedTile = new TileId(id);
        SelectedTileLabel.Text = $"Selected: {id}";
        TileSelected?.Invoke(SelectedTile);

        // Move selection highlight.
        Avalonia.Controls.Canvas.SetLeft(SelectionBorder, tileX * tileW);
        Avalonia.Controls.Canvas.SetTop(SelectionBorder,  tileY * tileH);
        SelectionBorder.Width     = tileW;
        SelectionBorder.Height    = tileH;
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
        }
    }
}
