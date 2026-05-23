using System.Reflection;
using Engine.Core.Tiles;
using Engine.Runtime.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Viewport;

/// <summary>
/// MonoGame game that renders each frame to an offscreen <see cref="RenderTarget2D"/>
/// and exposes the RGBA pixel buffer so the Avalonia editor can blit it to a WriteableBitmap.
/// <para>
/// On macOS, SDL2's Cocoa back-end must run on the main thread, so this class must be
/// initialized via <see cref="StartManual"/> (called from the Avalonia UI thread) and ticked
/// via <see cref="TickPublic"/> from a DispatcherTimer rather than using <see cref="Game.Run()"/>.
/// </para>
/// </summary>
public class ViewportGame : EngineGame
{
    // DoInitialize is internal; BeginRun is protected — both require reflection.
    // Tick() is public in MonoGame 3.8 and called directly.
    private static readonly MethodInfo? s_doInitialize =
        typeof(Game).GetMethod("DoInitialize", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? s_beginRun =
        typeof(Game).GetMethod("BeginRun", BindingFlags.Instance | BindingFlags.NonPublic);

    private RenderTarget2D? _target;
    private Color[]?        _colorBuffer;
    private byte[]?         _frameBuffer;
    private readonly object _frameLock = new();
    private int _targetWidth;
    private int _targetHeight;

    private volatile int _requestedWidth  = 800;
    private volatile int _requestedHeight = 600;

    private float _mouseX;
    private float _mouseY;
    private readonly object _mouseLock = new();

    // ── Camera ────────────────────────────────────────────────────────────────

    // Set by the editor each tick before TickPublic so the render is always fresh.
    // All access is on the Avalonia main thread (DispatcherTimer + TickPublic), so no locking needed.
    private float _cameraPanX;
    private float _cameraPanY;
    private int   _cameraZoom = 1;

    /// <summary>
    /// Updates the camera transform used in the next Draw call.
    /// <paramref name="panX"/>/<paramref name="panY"/> are in world pixels;
    /// <paramref name="zoom"/> must be a positive integer (1, 2, 4, 8 …).
    /// </summary>
    public void SetCamera(float panX, float panY, int zoom)
    {
        _cameraPanX = panX;
        _cameraPanY = panY;
        _cameraZoom = zoom > 0 ? zoom : 1;
    }

    // ── Tilemap rendering ─────────────────────────────────────────────────────

    private SpriteBatch?    _spriteBatch;
    private TilemapRenderer? _tilemapRenderer;
    private Texture2D?      _tilesetTexture;

    // Queued setup applied on the next Update tick (must be on the MonoGame/main thread).
    private (TilemapData Layer, TilesetData Tileset, string Path)? _pendingSetup;

    /// <summary>True once <see cref="StartManual"/> has completed successfully.</summary>
    public bool IsReady { get; private set; }

    public ViewportGame()
    {
        // 1×1 SDL back-buffer; the real output goes through the render target.
        _graphics.PreferredBackBufferWidth  = 1;
        _graphics.PreferredBackBufferHeight = 1;
        IsMouseVisible = false;
    }

    // ── Tilemap public API ────────────────────────────────────────────────────

    /// <summary>
    /// Queues a tilemap layer + tileset to load on the next tick.
    /// The texture is created from <paramref name="imagePath"/> inside the Update loop
    /// so it runs on the GraphicsDevice thread.
    /// </summary>
    public void SetTilemapSetup(TilemapData layer, TilesetData tileset, string imagePath)
    {
        _pendingSetup = (layer, tileset, imagePath);
    }

    // ── MonoGame overrides ────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the MonoGame platform (SDL + GraphicsDevice) synchronously on the calling
    /// thread. Must be called from the UI/main thread on macOS so SDL2's Cocoa back-end can
    /// create its window. Non-blocking — does NOT start a game loop.
    /// </summary>
    public void StartManual()
    {
        s_doInitialize?.Invoke(this, null); // Platform.BeforeInitialize() + Initialize()
        s_beginRun?.Invoke(this, null);     // BeginRun() — virtual no-op in EngineGame
        IsReady = true;
    }

    /// <summary>Runs one MonoGame Update+Draw tick. Call from the UI thread each frame.</summary>
    public void TickPublic()
    {
        if (IsReady) Tick(); // Game.Tick() is public in MonoGame 3.8
    }

    protected override void Initialize()
    {
        base.Initialize();
        Window.Position = new Point(-32000, -32000);
        CreateTarget(_requestedWidth, _requestedHeight);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        int rw = _requestedWidth, rh = _requestedHeight;
        if (rw != _targetWidth || rh != _targetHeight)
            CreateTarget(rw, rh);

        // Process any pending tilemap setup (texture load must be on the MonoGame thread).
        if (_pendingSetup is { } setup)
        {
            _pendingSetup = null;
            try
            {
                _tilesetTexture?.Dispose();
                using var stream = File.OpenRead(setup.Path);
                _tilesetTexture = Texture2D.FromStream(GraphicsDevice, stream);

                _tilemapRenderer?.Dispose();
                var ctx = new TilemapLayerContext
                {
                    Layer   = setup.Layer,
                    Tileset = setup.Tileset,
                    Texture = _tilesetTexture,
                };
                _tilemapRenderer = new TilemapRenderer([ctx]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ViewportGame] Failed to load tileset texture: {ex.Message}");
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_target is null)
        {
            base.Draw(gameTime);
            return;
        }

        // Render scene content into the offscreen render target.
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(new Color(20, 20, 28));
        DrawViewportContent(gameTime);

        // Restore the 1×1 back-buffer, then GPU → CPU readback.
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _target.GetData(_colorBuffer!);

        int count = _targetWidth * _targetHeight;
        var bytes = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            ref var c = ref _colorBuffer![i];
            bytes[i * 4 + 0] = c.R;
            bytes[i * 4 + 1] = c.G;
            bytes[i * 4 + 2] = c.B;
            bytes[i * 4 + 3] = 255;
        }

        lock (_frameLock)
            _frameBuffer = bytes;

        base.Draw(gameTime);
    }

    /// <summary>Override to render scene content into the viewport render target.</summary>
    protected virtual void DrawViewportContent(GameTime gameTime)
    {
        if (_tilemapRenderer is null || _spriteBatch is null) return;

        float panX = _cameraPanX;
        float panY = _cameraPanY;
        int   zoom = _cameraZoom;

        // Visible world-pixel rectangle used for chunk frustum culling.
        var worldCamera = new Rectangle(
            (int)panX,
            (int)panY,
            (int)System.Math.Ceiling((double)_targetWidth  / zoom),
            (int)System.Math.Ceiling((double)_targetHeight / zoom));

        // SpriteBatch transform: shift by -pan then scale by zoom.
        // Tiles are stored in world-pixel coords; the matrix converts them to screen pixels.
        var matrix = Matrix.CreateTranslation(-panX, -panY, 0f)
                   * Matrix.CreateScale(zoom, zoom, 1f);

        var clock = new TilemapClock((long)gameTime.TotalGameTime.TotalMilliseconds);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: matrix);
        _tilemapRenderer.Render(worldCamera, _spriteBatch, ctx => ctx.Texture, clock);
        _spriteBatch.End();
    }

    // ── Public thread-safe API ────────────────────────────────────────────────

    /// <summary>Requests a viewport resize; applied on the next tick.</summary>
    public void RequestResize(int width, int height)
    {
        if (width > 0 && height > 0)
        {
            _requestedWidth  = width;
            _requestedHeight = height;
        }
    }

    /// <summary>Forwards the Avalonia pointer position into the game.</summary>
    public void UpdateMousePosition(float x, float y)
    {
        lock (_mouseLock) { _mouseX = x; _mouseY = y; }
    }

    /// <summary>Returns the last forwarded mouse position.</summary>
    public (float X, float Y) MousePosition
    {
        get { lock (_mouseLock) return (_mouseX, _mouseY); }
    }

    /// <summary>
    /// Copies the latest RGBA frame into <paramref name="dest"/>.
    /// Returns true and sets <paramref name="width"/>/<paramref name="height"/> on success.
    /// </summary>
    public bool TryGetFrame(byte[] dest, out int width, out int height)
    {
        lock (_frameLock)
        {
            width  = _targetWidth;
            height = _targetHeight;
            if (_frameBuffer is null || dest.Length < _frameBuffer.Length)
                return false;
            Buffer.BlockCopy(_frameBuffer, 0, dest, 0, _frameBuffer.Length);
            return true;
        }
    }

    /// <summary>Byte length of the current frame (width × height × 4), or 0 while initializing.</summary>
    public int FrameByteSize
    {
        get { lock (_frameLock) return _frameBuffer?.Length ?? 0; }
    }

    private void CreateTarget(int width, int height)
    {
        _target?.Dispose();
        _target      = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        _colorBuffer = new Color[width * height];
        _targetWidth  = width;
        _targetHeight = height;
        lock (_frameLock)
            _frameBuffer = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _target?.Dispose();
            _spriteBatch?.Dispose();
            _tilemapRenderer?.Dispose();
            _tilesetTexture?.Dispose();
        }
        base.Dispose(disposing);
    }
}
