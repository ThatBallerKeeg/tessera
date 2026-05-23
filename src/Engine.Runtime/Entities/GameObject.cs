using Engine.Core.Math;

namespace Engine.Runtime.Entities;

/// <summary>
/// A live entity in the runtime scene. Holds a flat list of <see cref="Component"/>s;
/// no scene-graph parenting in v0.1.
/// </summary>
public sealed class GameObject
{
    private readonly List<Component> _components = new();

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Stable GUID — matches the <c>GameObjectData.Id</c> it was loaded from.</summary>
    public Guid Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    /// <summary>World-space position used for Y-sort depth and sprite placement.</summary>
    public Vector2 Position { get; set; }

    // ── Components ────────────────────────────────────────────────────────────

    /// <summary>Ordered list of components attached to this object.</summary>
    public IReadOnlyList<Component> Components => _components;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Creates a new GameObject with the given stable identity.</summary>
    public GameObject(Guid id, string name)
    {
        Id   = id;
        Name = name;
    }

    // ── Component management ──────────────────────────────────────────────────

    /// <summary>
    /// Appends <paramref name="component"/> and calls <see cref="Component.OnAttach"/>.
    /// </summary>
    public void AddComponent(Component component)
    {
        _components.Add(component);
        component.Owner = this;
        component.OnAttach(this);
    }

    /// <summary>
    /// Returns the first component of type <typeparamref name="T"/>, or null if none is attached.
    /// </summary>
    public T? GetComponent<T>() where T : Component
    {
        foreach (var c in _components)
            if (c is T match) return match;
        return null;
    }

    /// <summary>
    /// Removes <paramref name="component"/> and calls <see cref="Component.OnDetach"/>.
    /// Returns false if the component was not attached.
    /// </summary>
    public bool RemoveComponent(Component component)
    {
        if (!_components.Remove(component)) return false;
        component.OnDetach();
        component.Owner = null;
        return true;
    }

    // ── Per-frame dispatch ────────────────────────────────────────────────────

    /// <summary>Ticks every attached component in authoring order.</summary>
    internal void Update(float deltaSeconds)
    {
        // Iterate by index; components should not add/remove siblings during Update.
        for (int i = 0; i < _components.Count; i++)
            _components[i].OnUpdate(deltaSeconds);
    }

    /// <summary>Calls <see cref="Component.OnDraw"/> on every attached component.</summary>
    internal void Draw(ISpriteBatch spriteBatch)
    {
        for (int i = 0; i < _components.Count; i++)
            _components[i].OnDraw(spriteBatch);
    }
}
