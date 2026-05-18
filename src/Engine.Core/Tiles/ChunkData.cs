namespace Engine.Core.Tiles;

/// <summary>
/// Storage for one 16×16 tile chunk. Internal implementation detail of <see cref="TilemapData"/>;
/// external consumers read tiles via <see cref="TilemapData.GetTile"/> or
/// <see cref="TilemapData.TryGetChunkTiles"/>.
/// </summary>
public sealed class ChunkData
{
    /// <summary>Flat tile array, row-major. Index = localY * ChunkSize + localX.</summary>
    public TileId[] Tiles { get; set; } = new TileId[ChunkCoord.TileCount];

    /// <summary>Number of non-empty tiles in this chunk. Maintained by TilemapData.</summary>
    public int NonEmptyCount { get; set; }
}
