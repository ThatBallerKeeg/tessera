using Engine.Core.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime;

/// <summary>
/// Core MonoGame entry point for the engine runtime.
/// Game projects inherit from or host this class; the editor embeds it in a viewport panel (Phase 1).
/// </summary>
public class EngineGame : Game
{
    protected readonly GraphicsDeviceManager _graphics;
    private SceneData? _currentScene;

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

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 20, 28));
        base.Draw(gameTime);
    }

    /// <summary>
    /// Loads <paramref name="scene"/> into the runtime.
    /// Prints the scene name and GameObject count to the console (placeholder; removed in Phase 1).
    /// </summary>
    public void LoadScene(SceneData scene)
    {
        _currentScene = scene;

        // Walk the scene — Phase 3 instantiates runtime component types for each GameObject.
        foreach (GameObjectData _ in _currentScene.GameObjects)
        {
        }

        Console.WriteLine(
            $"[Engine.Runtime] Scene '{_currentScene.Name}' loaded: " +
            $"{_currentScene.GameObjects.Count} GameObject(s).");
    }
}
