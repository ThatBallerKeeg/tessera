using Engine.Core.Math;

namespace Engine.Core.Tiles;

/// <summary>
/// Sparse, chunked tilemap layer. Chunks are created on demand and pruned when empty.
/// Use <see cref="BeginBulkEdit"/> when writing many tiles at once (e.g. procgen) to
/// batch <see cref="ChunkDirty"/> notifications.
/// </summary>
public sealed class TilemapData
{
    /// <summary>Display name of this layer, e.g. "floor", "walls", "decor".</summary>
    public string LayerName { get; set; } = "default";

    /// <summary>Render order among layers. Layers are drawn ascending by index.</summary>
    public int LayerIndex { get; set; }

    /// <summary>The tileset asset this layer draws from.</summary>
    public Guid TilesetRef { get; set; }

    /// <summary>Sparse chunk storage. Keys are chunk coordinates; values are chunk tile data.</summary>
    public Dictionary<ChunkCoord, ChunkData> Chunks { get; set; } = new();

    /// <summary>
    /// Fired when a chunk's tiles change. During a <see cref="BeginBulkEdit"/> scope the event
    /// is deferred; one event per affected chunk fires when the outermost scope is disposed.
    /// </summary>
    public event Action<ChunkCoord>? ChunkDirty;

    private int _bulkDepth;
    private HashSet<ChunkCoord>? _pendingDirty;

    // ── Editing API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an <see cref="IDisposable"/> scope that batches <see cref="ChunkDirty"/>
    /// notifications. Individual <see cref="SetTile"/> calls inside the scope accumulate
    /// per-chunk dirty flags; on disposal one event fires per affected chunk.
    /// Scopes may be nested; events fire when the outermost scope is disposed.
    /// </summary>
    public IDisposable BeginBulkEdit()
    {
        if (_bulkDepth == 0)
            _pendingDirty = new HashSet<ChunkCoord>();
        _bulkDepth++;
        return new BulkEditScope(this);
    }

    /// <summary>Sets the tile at <paramref name="worldPos"/> to <paramref name="tile"/>.</summary>
    public void SetTile(Vector2Int worldPos, TileId tile)
    {
        var coord = ChunkCoord.FromWorld(worldPos);
        int idx   = ChunkCoord.LocalIndex(worldPos);

        if (!Chunks.TryGetValue(coord, out var chunk))
        {
            if (tile == TileId.Empty) return; // nothing to do, no chunk exists
            chunk = new ChunkData();
            Chunks[coord] = chunk;
        }

        var old = chunk.Tiles[idx];
        if (old == tile) return; // no-op

        chunk.Tiles[idx] = tile;

        if (old == TileId.Empty)
        {
            chunk.NonEmptyCount++;
        }
        else if (tile == TileId.Empty && --chunk.NonEmptyCount == 0)
        {
            Chunks.Remove(coord); // prune chunk when it becomes fully empty
        }

        MarkDirty(coord);
    }

    /// <summary>Returns the tile at <paramref name="worldPos"/>, or <see cref="TileId.Empty"/> if unset.</summary>
    public TileId GetTile(Vector2Int worldPos)
    {
        var coord = ChunkCoord.FromWorld(worldPos);
        return Chunks.TryGetValue(coord, out var chunk)
            ? chunk.Tiles[ChunkCoord.LocalIndex(worldPos)]
            : TileId.Empty;
    }

    // ── Read API for the renderer ─────────────────────────────────────────────

    /// <summary>Returns the chunk coordinates of every non-empty chunk.</summary>
    public IEnumerable<ChunkCoord> OccupiedChunks => Chunks.Keys;

    /// <summary>
    /// Copies the tile array for <paramref name="coord"/> into <paramref name="dest"/>.
    /// <paramref name="dest"/> must have length ≥ <see cref="ChunkCoord.TileCount"/>.
    /// Returns false if the chunk doesn't exist (all tiles are empty).
    /// </summary>
    public bool TryGetChunkTiles(ChunkCoord coord, TileId[] dest)
    {
        if (!Chunks.TryGetValue(coord, out var chunk)) return false;
        chunk.Tiles.CopyTo(dest, 0);
        return true;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void MarkDirty(ChunkCoord coord)
    {
        if (_bulkDepth > 0)
            _pendingDirty!.Add(coord);
        else
            ChunkDirty?.Invoke(coord);
    }

    private void EndBulkEdit()
    {
        if (--_bulkDepth == 0 && _pendingDirty != null)
        {
            foreach (var coord in _pendingDirty)
                ChunkDirty?.Invoke(coord);
            _pendingDirty.Clear();
        }
    }

    private sealed class BulkEditScope : IDisposable
    {
        private readonly TilemapData _owner;
        private bool _disposed;

        internal BulkEditScope(TilemapData owner) => _owner = owner;

        public void Dispose()
        {
            if (!_disposed) { _disposed = true; _owner.EndBulkEdit(); }
        }
    }
}
