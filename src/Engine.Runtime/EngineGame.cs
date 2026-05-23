using Engine.Core.Scene;
using Engine.Runtime.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime;

/// <summary>
/// Core MonoGame entry point for the engine runtime.
/// Game projects inherit from or host this class; the editor embeds it in a viewport panel.
/// </summary>
public class EngineGame : Game
{
    protected readonly GraphicsDeviceManager _graphics;

    // ── Scene ─────────────────────────────────────────────────────────────────

    /// <summary>The currently active live scene (null until <see cref="LoadScene"/> is called).</summary>
    protected Scene? _activeScene;

    // ── Rendering (standalone game path) ─────────────────────────────────────

    // ViewportGame overrides Draw entirely (renders to an offscreen RenderTarget).
    // These fields are only used by the standalone EngineGame.Draw path.
    private SpriteBatch?        _sceneBatch;
    private SpriteBatchAdapter? _sceneBatchAdapter;

    // ── Construction ──────────────────────────────────────────────────────────

    public EngineGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth  = 1280,
            PreferredBackBufferHeight = 720,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    // ── MonoGame lifecycle ────────────────────────────────────────────────────

    protected override void Initialize()
    {
        base.Initialize();
        _sceneBatch        = new SpriteBatch(GraphicsDevice);
        _sceneBatchAdapter = new SpriteBatchAdapter(_sceneBatch);
    }

    /// <summary>
    /// Ticks the active scene: for each GameObject, for each Component,
    /// calls <see cref="Component.OnUpdate"/> with wall-clock delta in seconds.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _activeScene?.Update(delta);
        base.Update(gameTime);
    }

    /// <summary>
    /// Renders the scene:
    /// <list type="number">
    ///   <item><description>Clear to background color.</description></item>
    ///   <item><description>Tilemap layers (handled by ViewportGame / Phase 2.7 for standalone).</description></item>
    ///   <item><description>GameObjects in Y-ascending order via <see cref="Scene.Draw"/>.</description></item>
    /// </list>
    /// ViewportGame overrides this entirely for the offscreen render-target path.
    /// </summary>
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 20, 28));

        // Tilemap rendering: ViewportGame handles via TilemapRenderer.
        // TODO(Phase 2.7): add standalone tilemap rendering here.

        // Entity rendering: Y-sorted OnDraw pass.
        if (_activeScene is not null)
        {
            _sceneBatch!.Begin(samplerState: SamplerState.PointClamp);
            _activeScene.Draw(_sceneBatchAdapter!);
            _sceneBatch.End();
        }

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _sceneBatch?.Dispose();
        base.Dispose(disposing);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates a live <see cref="Scene"/> from <paramref name="data"/> via
    /// <see cref="SceneLoader"/> and sets it as the active scene.
    /// </summary>
    public void LoadScene(SceneData data)
    {
        _activeScene = SceneLoader.Load(data);
        Console.WriteLine(
            $"[Engine.Runtime] Scene '{data.Name}' loaded: " +
            $"{_activeScene.GameObjects.Count} GameObject(s), " +
            $"{_activeScene.TilemapLayers.Count} tilemap layer(s).");
    }
}
