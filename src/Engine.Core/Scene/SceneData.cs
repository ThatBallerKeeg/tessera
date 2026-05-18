using Engine.Core.Tiles;

namespace Engine.Core.Scene;

/// <summary>
/// On-disk representation of a scene.
/// Every saved file carries <see cref="FormatVersion"/> so loaders can migrate forward.
/// The live runtime counterpart (with behavior) is assembled in Phase 3.
/// </summary>
public sealed class SceneData
{
    /// <summary>Scene file format version. Incremented on breaking changes; loaders migrate old files forward.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Display name of the scene.</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Root-level GameObjects in the scene.</summary>
    public List<GameObjectData> GameObjects { get; set; } = new();

    /// <summary>Tilemap layers in the scene, ordered by <see cref="TilemapData.LayerIndex"/>.</summary>
    public List<TilemapData> Tilemaps { get; set; } = new();
}
