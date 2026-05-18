using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Engine.Core.Tiles;
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

    // ── Events fired by the viewport ──────────────────────────────────────────

    /// <summary>Fired on left-button press. Point is in viewport pixel space (1:1 with world at v0.1).</summary>
    public event Action<Point>? ViewportPointerPressed;

    /// <summary>Fired on pointer move while left button is held.</summary>
    public event Action<Point>? ViewportPointerDragged;

    /// <summary>Fired on left-button release.</summary>
    public event Action? ViewportPointerReleased;

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

        SizeChanged                += OnSizeChanged;
        ViewportImage.PointerMoved += OnPointerMoved;
        ViewportImage.PointerPressed  += OnPointerPressed;
        ViewportImage.PointerReleased += OnPointerReleased;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _pollTimer?.Stop();
        _game?.Exit();
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

    // ── Frame loop ────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_game is null || !_game.IsReady) return;

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

        // Notify Avalonia that the Image's pixel data changed so it repaints this tick.
        ViewportImage.InvalidateVisual();
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(ViewportImage);
        _game?.UpdateMousePosition((float)pos.X, (float)pos.Y);

        if (_isPointerDown)
            ViewportPointerDragged?.Invoke(pos);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ViewportImage).Properties.IsLeftButtonPressed) return;
        _isPointerDown = true;
        ViewportPointerPressed?.Invoke(e.GetPosition(ViewportImage));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPointerDown = false;
        ViewportPointerReleased?.Invoke();
    }
}
