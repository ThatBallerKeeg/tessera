namespace Engine.Core.Tiles;

/// <summary>Per-tile metadata stored in a <see cref="TilesetData"/>.</summary>
public sealed class TileMetadata
{
    /// <summary>The tile this metadata describes.</summary>
    public TileId Id { get; set; }

    /// <summary>Whether this tile blocks entity movement.</summary>
    public bool Solid { get; set; }

    /// <summary>Movement speed multiplier applied when walking on this tile. 1.0 = normal.</summary>
    public float Friction { get; set; } = 1.0f;

    /// <summary>Autotile terrain group, e.g. "grass", "dirt", "water".</summary>
    public string TerrainTag { get; set; } = "";

    /// <summary>Higher priority wins at terrain edges. Used by multi-terrain autotile resolution.</summary>
    public int TerrainPriority { get; set; }

    /// <summary>
    /// Which of the 47 blob-autotile variants this tile represents (0–46).
    /// -1 means this tile is not part of an autotile set.
    /// </summary>
    public int BlobVariant { get; set; } = -1;

    /// <summary>Animation frames. Empty list = static tile.</summary>
    public List<TileFrame> Frames { get; set; } = new();
}
