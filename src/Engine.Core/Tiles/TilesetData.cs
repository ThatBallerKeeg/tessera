using Engine.Core.Math;

namespace Engine.Core.Tiles;

/// <summary>
/// Asset describing a tileset: the source image and per-tile metadata.
/// One tileset can have its own tile size independent of other tilesets in the project.
/// </summary>
public sealed class TilesetData
{
    /// <summary>Display name of this tileset.</summary>
    public string Name { get; set; } = "";

    /// <summary>Path to the tileset image, relative to the project root.</summary>
    public string ImagePath { get; set; } = "";

    /// <summary>Width and height of each tile in pixels.</summary>
    public Vector2Int TileSize { get; set; } = new(16, 16);

    /// <summary>Per-tile metadata entries, one per tile in the tileset image.</summary>
    public List<TileMetadata> Tiles { get; set; } = new();
}
