using Engine.Core.Math;

namespace Engine.Core.Scene;

/// <summary>On-disk representation of a GameObject.</summary>
public sealed class GameObjectData
{
    /// <summary>Stable identity used to correlate instances across saves and prefab diffs.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "GameObject";

    /// <summary>World-space position.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Components attached to this GameObject, in authoring order.</summary>
    public List<ComponentData> Components { get; set; } = new();
}
