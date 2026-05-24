using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;   // Bitmap, RenderTargetBitmap, WriteableBitmap
using Avalonia.Platform;
using Avalonia.Threading;
using Engine.Core.Sprites;
using Engine.Core.Tiles;
using Engine.Runtime.Entities;
using Engine.Runtime.Viewport;

namespace Engine.Editor.Viewport;

/// <summary>
/// Avalonia control that hosts a <see cref="ViewportGame"/> and displays its output.
/// <para>
/// Strategy: Option 1 — offscreen RenderTarget2D → WriteableBitmap (CPU↔GPU bounce,
/// ~60 fps for editor preview). The MonoGame game loop runs on the Avalonia main thread
/// via <see cref="DispatcherTimer"/> so SDL2's Cocoa back-end (macOS) can process events.
/// </para>
/// </summary>
public partial class MonoGameViewport : UserControl
{
    private ViewportGame?    _game;
    private WriteableBitmap? _bitmap;
    private byte[]?          _stagingBuffer;
    private int              _bitmapW, _bitmapH;
    private DispatcherTimer? _pollTimer;
    private bool             _started;
    private bool             _isPointerDown;

    // Active tile size — set by the editor when a tileset is activated.
    private int _tileW, _tileH;

    // ── Camera state ──────────────────────────────────────────────────────────
    private static readonly int[] ZoomLevels = { 1, 2, 4, 8 };
    private int   _zoomIndex;          // index into ZoomLevels
    private int   _zoom = 1;           // ZoomLevels[_zoomIndex]
    private float _panX, _panY;        // top-left world-pixel of the viewport

    /// <summary>Current discrete zoom factor (1, 2, 4, or 8).</summary>
    public int   Zoom => _zoom;
    /// <summary>Horizontal pan offset in world pixels.</summary>
    public float PanX => _panX;
    /// <summary>Vertical pan offset in world pixels.</summary>
    public float PanY => _panY;

    // ── Pan input state ───────────────────────────────────────────────────────
    private bool  _isPanning;
    private float _panStartX, _panStartY;         // _panX/Y when drag started
    private float _panStartMouseX, _panStartMouseY; // screen pos when drag started
    private bool  _isSpaceDown;
    private TopLevel? _topLevel;                  // for key subscription

    // ── Preview shapes on the overlay canvas ─────────────────────────────────
    // z-order (back → front): ghost, rectPreview, linePreview, cursorRect.
    // Ghost uses Children.Insert(0) so it stays below all other overlays.
    private Rectangle? _rectPreview;
    private Line?      _linePreview;
    private Rectangle? _cursorRect;

    // ── Ghost tile preview ────────────────────────────────────────────────────
    private bool   _ghostEnabled;
    private int    _ghostTileW, _ghostTileH;  // source tile size in pixels
    private Image? _ghostImage;               // Avalonia Image element on the overlay canvas

    // ── Events fired by the viewport ──────────────────────────────────────────

    /// <summary>Fired on left-button press. Point is in viewport pixel space (1:1 with world at v0.1).</summary>
    public event Action<Point, KeyModifiers>? ViewportPointerPressed;

    /// <summary>Fired on pointer move while left button is held.</summary>
    public event Action<Point>? ViewportPointerDragged;

    /// <summary>Fired on left-button release. Point is in viewport pixel space.</summary>
    public event Action<Point>? ViewportPointerReleased;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public MonoGameViewport()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        _game = new ViewportGame();
        _game.StartManual();

        if (Bounds is { Width: > 1, Height: > 1 })
            _game.RequestResize((int)Bounds.Width, (int)Bounds.Height);

        _pollTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _pollTimer.Tick += OnTick;
        _pollTimer.Start();

        SizeChanged                       += OnSizeChanged;
        ViewportImage.PointerMoved        += OnPointerMoved;
        ViewportImage.PointerPressed      += OnPointerPressed;
        ViewportImage.PointerReleased     += OnPointerReleased;
        ViewportImage.PointerExited       += OnPointerExited;
        ViewportImage.PointerWheelChanged += OnPointerWheelChanged;

        // Track the Space key globally so pan works without the control having focus.
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.KeyDown += OnTopLevelKeyDown;
            _topLevel.KeyUp   += OnTopLevelKeyUp;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _pollTimer?.Stop();
        _game?.Exit();
        if (_topLevel is not null)
        {
            _topLevel.KeyDown -= OnTopLevelKeyDown;
            _topLevel.KeyUp   -= OnTopLevelKeyUp;
            _topLevel = null;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        int w = Math.Max(1, (int)e.NewSize.Width);
        int h = Math.Max(1, (int)e.NewSize.Height);
        _game?.RequestResize(w, h);
    }

    // ── Tilemap support ───────────────────────────────────────────────────────

    /// <summary>
    /// Queues a tilemap layer + tileset to be loaded on the next MonoGame tick.
    /// The texture is loaded from <paramref name="imagePath"/> inside the MonoGame Update loop.
    /// </summary>
    public void LoadTilemapLayer(TilemapData layer, TilesetData tileset, string imagePath)
    {
        _game?.SetTilemapSetup(layer, tileset, imagePath);
    }

    // ── Play-mode support ─────────────────────────────────────────────────────

    /// <summary>
    /// Starts running <paramref name="scene"/> in the viewport.
    /// If <paramref name="sheet"/> and <paramref name="imagePath"/> are provided the PNG is
    /// loaded on the next MonoGame tick and wired to all SpriteRenderers in the scene.
    /// </summary>
    public void StartPlay(Scene? scene, SpritesheetData? sheet, string? imagePath)
        => _game?.StartPlay(scene, sheet, imagePath);

    /// <summary>Stops play mode and releases the scene's sprite texture.</summary>
    public void StopPlay()
        => _game?.StopPlay();

    // ── Preview canvas ────────────────────────────────────────────────────────

    /// <summary>Shows a rectangle outline preview snapped to tile grid.</summary>
    public void UpdateRectPreview(int tx1, int ty1, int tx2, int ty2, int tileW, int tileH)
    {
        // World-pixel extents of the selection.
        float wx = System.Math.Min(tx1, tx2) * tileW;
        float wy = System.Math.Min(ty1, ty2) * tileH;
        float ww = (System.Math.Abs(tx1 - tx2) + 1) * tileW;
        float wh = (System.Math.Abs(ty1 - ty2) + 1) * tileH;

        if (_rectPreview is null)
        {
            _rectPreview = new Rectangle
            {
                Stroke          = Brushes.Yellow,
                StrokeThickness = 1,
                Fill            = Brushes.Transparent,
            };
            PreviewCanvas.Children.Add(_rectPreview);
        }

        Canvas.SetLeft(_rectPreview, (wx - _panX) * _zoom);
        Canvas.SetTop(_rectPreview,  (wy - _panY) * _zoom);
        _rectPreview.Width     = ww * _zoom;
        _rectPreview.Height    = wh * _zoom;
        _rectPreview.IsVisible = true;
    }

    /// <summary>Shows a straight line preview from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public void UpdateLinePreview(Point start, Point end)
    {
        if (_linePreview is null)
        {
            _linePreview = new Line
            {
                Stroke          = Brushes.Yellow,
                StrokeThickness = 1,
            };
            PreviewCanvas.Children.Add(_linePreview);
        }

        _linePreview.StartPoint = start;
        _linePreview.EndPoint   = end;
        _linePreview.IsVisible  = true;
    }

    /// <summary>Hides all preview shapes (rect and line). Does not affect the tile cursor.</summary>
    public void ClearPreview()
    {
        if (_rectPreview is not null) _rectPreview.IsVisible = false;
        if (_linePreview is not null) _linePreview.IsVisible = false;
    }

    // ── Tile cursor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tells the viewport what tile size to use for the hover cursor highlight.
    /// Call whenever the active tileset changes. Pass 0 to disable the cursor.
    /// </summary>
    public void SetTileSize(int tileW, int tileH)
    {
        _tileW = tileW;
        _tileH = tileH;
        if (tileW <= 0 || tileH <= 0)
            HideTileCursor();
    }

    private static readonly ISolidColorBrush CursorBrush =
        new SolidColorBrush(Color.FromArgb(180, 255, 255, 120));

    private void UpdateTileCursor(int tx, int ty)
    {
        if (_cursorRect is null)
        {
            _cursorRect = new Rectangle
            {
                Stroke          = CursorBrush,
                StrokeThickness = 1,
                Fill            = Brushes.Transparent,
            };
            PreviewCanvas.Children.Add(_cursorRect);
        }

        Canvas.SetLeft(_cursorRect, (tx * _tileW - _panX) * _zoom);
        Canvas.SetTop(_cursorRect,  (ty * _tileH - _panY) * _zoom);
        _cursorRect.Width     = _tileW * _zoom;
        _cursorRect.Height    = _tileH * _zoom;
        _cursorRect.IsVisible = true;
    }

    private void HideTileCursor()
    {
        if (_cursorRect is not null) _cursorRect.IsVisible = false;
    }

    // ── Ghost tile preview ────────────────────────────────────────────────────

    /// <summary>
    /// Sets the ghost tile that follows the cursor in Paint mode.
    /// Pass <paramref name="tilesetBitmap"/> = null (or zero-sized tile) to disable the ghost.
    /// <paramref name="srcX"/>/<paramref name="srcY"/> are in source-image pixels.
    /// </summary>
    public void SetGhostTile(Bitmap? tilesetBitmap, int srcX, int srcY, int tileW, int tileH)
    {
        _ghostEnabled = tilesetBitmap is not null && tileW > 0 && tileH > 0;
        _ghostTileW   = tileW;
        _ghostTileH   = tileH;

        if (!_ghostEnabled)
        {
            HideGhost();
            return;
        }

        // Explicitly render just the tile's pixels into a small RenderTargetBitmap.
        // This avoids ImageBrush.SourceRect entirely: in Avalonia 11, SourceRect with
        // RelativeUnit.Absolute does not visibly crop the image — the Rectangle shows
        // the full (invisible/unclipped) brush.  Drawing the crop into a dedicated
        // bitmap and assigning it to an Image.Source is the reliable alternative.
        var tileBitmap = new RenderTargetBitmap(
            new PixelSize(tileW, tileH), new Vector(96, 96));
        using (var ctx = tileBitmap.CreateDrawingContext())
        {
            ctx.DrawImage(
                tilesetBitmap!,
                new Rect(srcX, srcY, tileW, tileH),   // source crop in tileset pixels
                new Rect(0, 0, tileW, tileH));          // fill the whole render target
        }

        if (_ghostImage is null)
        {
            _ghostImage = new Image
            {
                Opacity          = 0.5,
                IsHitTestVisible = false,
                Stretch          = Stretch.Fill,
            };
            RenderOptions.SetBitmapInterpolationMode(_ghostImage, BitmapInterpolationMode.None);
            // Insert at index 0 so the ghost renders behind the cursor outline.
            PreviewCanvas.Children.Insert(0, _ghostImage);
        }

        // Dispose the previous RenderTargetBitmap now that it's no longer referenced.
        (_ghostImage.Source as RenderTargetBitmap)?.Dispose();
        _ghostImage.Source = tileBitmap;
        // Ghost position/size updated on the next pointer-move event.
    }

    private void UpdateGhostPosition(int tx, int ty)
    {
        if (!_ghostEnabled || _ghostImage is null) return;

        Canvas.SetLeft(_ghostImage, (tx * _ghostTileW - _panX) * _zoom);
        Canvas.SetTop(_ghostImage,  (ty * _ghostTileH - _panY) * _zoom);
        _ghostImage.Width     = _ghostTileW * _zoom;
        _ghostImage.Height    = _ghostTileH * _zoom;
        _ghostImage.IsVisible = true;
    }

    private void HideGhost()
    {
        if (_ghostImage is not null) _ghostImage.IsVisible = false;
    }

    // ── Frame loop ────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_game is null || !_game.IsReady) return;

        _game.SetCamera(_panX, _panY, _zoom);
        _game.TickPublic();

        int needed = _game.FrameByteSize;
        if (needed == 0) return;

        if (_stagingBuffer is null || _stagingBuffer.Length < needed)
            _stagingBuffer = new byte[needed];

        if (!_game.TryGetFrame(_stagingBuffer, out int w, out int h))
            return;

        if (_bitmap is null || _bitmapW != w || _bitmapH != h)
        {
            _bitmap  = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Opaque);
            _bitmapW = w;
            _bitmapH = h;
            ViewportImage.Source = _bitmap;
        }

        using (var fb = _bitmap.Lock())
        {
            int srcRow = w * 4;
            if (fb.RowBytes == srcRow)
            {
                Marshal.Copy(_stagingBuffer, 0, fb.Address, w * h * 4);
            }
            else
            {
                for (int row = 0; row < h; row++)
                    Marshal.Copy(_stagingBuffer, row * srcRow, fb.Address + row * fb.RowBytes, srcRow);
            }
        }

        ViewportImage.InvalidateVisual();
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(ViewportImage);
        _game?.UpdateMousePosition((float)pos.X, (float)pos.Y);

        if (_isPanning)
        {
            _panX = _panStartX - (float)(pos.X - _panStartMouseX) / _zoom;
            _panY = _panStartY - (float)(pos.Y - _panStartMouseY) / _zoom;
        }

        // Always refresh the tile-cursor and ghost to reflect current camera + mouse position.
        if (_tileW > 0 && _tileH > 0)
        {
            int tx = (int)System.Math.Floor((pos.X / _zoom + _panX) / _tileW);
            int ty = (int)System.Math.Floor((pos.Y / _zoom + _panY) / _tileH);
            UpdateTileCursor(tx, ty);
            UpdateGhostPosition(tx, ty);
        }

        if (!_isPanning && _isPointerDown)
            ViewportPointerDragged?.Invoke(pos);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(ViewportImage).Properties;
        var pos   = e.GetPosition(ViewportImage);

        // Middle-button or Space+left → pan.
        if (props.IsMiddleButtonPressed || (_isSpaceDown && props.IsLeftButtonPressed))
        {
            _isPanning        = true;
            _panStartX        = _panX;
            _panStartY        = _panY;
            _panStartMouseX   = (float)pos.X;
            _panStartMouseY   = (float)pos.Y;
            e.Handled         = true;
            return;
        }

        if (!props.IsLeftButtonPressed) return;
        _isPointerDown = true;
        ViewportPointerPressed?.Invoke(pos, e.KeyModifiers);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            return;
        }
        _isPointerDown = false;
        ViewportPointerReleased?.Invoke(e.GetPosition(ViewportImage));
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isPanning = false;
        HideTileCursor();
        HideGhost();
    }

    // ── Zoom (mouse wheel) ────────────────────────────────────────────────────

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0) return;

        var pos    = e.GetPosition(ViewportImage);
        float mx   = (float)pos.X;
        float my   = (float)pos.Y;
        int oldIdx = _zoomIndex;

        if      (e.Delta.Y > 0) _zoomIndex = System.Math.Min(_zoomIndex + 1, ZoomLevels.Length - 1);
        else if (e.Delta.Y < 0) _zoomIndex = System.Math.Max(_zoomIndex - 1, 0);

        if (_zoomIndex == oldIdx) { e.Handled = true; return; }

        int oldZoom = ZoomLevels[oldIdx];
        int newZoom = ZoomLevels[_zoomIndex];

        // Keep the world point under the cursor fixed after zoom.
        float wx = mx / oldZoom + _panX;
        float wy = my / oldZoom + _panY;
        _zoom = newZoom;
        _panX = wx - mx / newZoom;
        _panY = wy - my / newZoom;

        // Refresh cursor and ghost overlays immediately.
        if (_tileW > 0 && _tileH > 0)
        {
            int tx = (int)System.Math.Floor((mx / _zoom + _panX) / _tileW);
            int ty = (int)System.Math.Floor((my / _zoom + _panY) / _tileH);
            UpdateTileCursor(tx, ty);
            UpdateGhostPosition(tx, ty);
        }

        e.Handled = true;
    }

    // ── Space key (global, for pan) ───────────────────────────────────────────

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Ignore key events consumed by text inputs (TextBox etc.).
        if (e.Key == Key.Space && !e.Handled)
            _isSpaceDown = true;
    }

    private void OnTopLevelKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            _isSpaceDown = false;
    }
}
