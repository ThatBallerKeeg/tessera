using Engine.Core.Tiles;

namespace Engine.Runtime.Entities;

/// <summary>
/// Live runtime scene: tilemap layers (data only; textures managed by the renderer) plus
/// the flat list of active <see cref="GameObject"/>s.
/// </summary>
public sealed class Scene
{
    private readonly List<GameObject> _gameObjects = new();
    private readonly List<GameObject> _drawOrder   = new(); // reused each frame to avoid per-frame alloc

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>Tilemap layers in layer-index order. Renderers build their own contexts from this.</summary>
    public IReadOnlyList<TilemapData> TilemapLayers { get; }

    /// <summary>All live GameObjects in the scene.</summary>
    public IReadOnlyList<GameObject> GameObjects => _gameObjects;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Creates a scene with the given ordered tilemap layers (may be empty).</summary>
    public Scene(IEnumerable<TilemapData> tilemapLayers)
    {
        TilemapLayers = tilemapLayers.ToList().AsReadOnly();
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>Adds <paramref name="gameObject"/> to the scene.</summary>
    public void AddGameObject(GameObject gameObject)
        => _gameObjects.Add(gameObject);

    /// <summary>
    /// Removes <paramref name="gameObject"/> from the scene.
    /// Returns false if it was not present.
    /// </summary>
    public bool RemoveGameObject(GameObject gameObject)
        => _gameObjects.Remove(gameObject);

    // ── Per-frame dispatch ────────────────────────────────────────────────────

    /// <summary>Ticks every GameObject in scene order (insertion order, not Y-sorted).</summary>
    public void Update(float deltaSeconds)
    {
        for (int i = 0; i < _gameObjects.Count; i++)
            _gameObjects[i].Update(deltaSeconds);
    }

    /// <summary>
    /// Draws every GameObject in ascending <see cref="GameObject.Position"/>.Y order
    /// (lower Y = further back = drawn first), giving fake isometric depth.
    /// </summary>
    public void Draw(ISpriteBatch spriteBatch)
    {
        // Copy to a scratch list and sort in-place; avoids LINQ allocation.
        _drawOrder.Clear();
        for (int i = 0; i < _gameObjects.Count; i++)
            _drawOrder.Add(_gameObjects[i]);

        _drawOrder.Sort(static (a, b) => a.Position.Y.CompareTo(b.Position.Y));

        for (int i = 0; i < _drawOrder.Count; i++)
            _drawOrder[i].Draw(spriteBatch);
    }
}
