namespace Engine.Core.Tiles;

/// <summary>One frame in an animated tile sequence.</summary>
public sealed class TileFrame
{
    /// <summary>Tile to display during this frame.</summary>
    public TileId TileId { get; set; }

    /// <summary>How long this frame is shown, in milliseconds.</summary>
    public int DurationMs { get; set; } = 200;
}
