using Engine.Core.Math;
using Engine.Core.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Engine.Runtime.Tiles;

/// <summary>
/// Chunked, frustum-culled tilemap renderer with animated-tile support.
/// <para>
/// Each chunk maintains a render cache of <c>(destRect, resolvedTileId)</c> pairs, built lazily
/// and invalidated via <see cref="TilemapData.ChunkDirty"/>. The resolved TileId stored in the
/// cache is the autotile variant — it never changes between frames. Animation is applied at
/// draw time from the <see cref="TilemapClock"/>, so the cache does not need to be rebuilt on
/// every tick.
/// </para>
/// <para>Layers are drawn in ascending <see cref="TilemapData.LayerIndex"/> order.</para>
/// </summary>
public sealed class TilemapRenderer : IDisposable
{
    private readonly List<LayerState> _layers;
    private readonly List<(TilemapData Data, Action<ChunkCoord> Handler)> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Fired each time a chunk's render cache is rebuilt.
    /// Useful for diagnostics and verifying cache-invalidation behaviour in tests.
    /// </summary>
    public event Action<ChunkCoord>? ChunkCacheRebuilt;

    /// <summary>
    /// Creates a renderer for the supplied layer contexts.
    /// Layers are sorted by <see cref="TilemapData.LayerIndex"/> ascending.
    /// </summary>
    public TilemapRenderer(IReadOnlyList<TilemapLayerContext> layers)
    {
        _layers = layers
            .OrderBy(ctx => ctx.Layer.LayerIndex)
            .Select(ctx => new LayerState(ctx))
            .ToList();

        foreach (var ls in _layers)
        {
            var captured = ls;
            Action<ChunkCoord> handler = coord => InvalidateChunk(captured, coord);
            ls.Context.Layer.ChunkDirty += handler;
            _subscriptions.Add((ls.Context.Layer, handler));
        }
    }

    // ── Render overloads ──────────────────────────────────────────────────────

    /// <summary>
    /// Renders all visible layers to <paramref name="drawer"/>.
    /// <paramref name="clock"/> drives animated-tile frame selection; pass <c>default</c>
    /// (ElapsedMs = 0) for static-only scenes or tests that don't need animation.
    /// </summary>
    public void Render(Rectangle cameraWorldPixels, ITileDrawer drawer,
                       TilemapClock clock = default)
    {
        foreach (var ls in _layers)
            RenderLayer(ls, cameraWorldPixels, drawer, clock);
    }

    /// <summary>
    /// Renders all visible layers to <paramref name="spriteBatch"/>.
    /// <paramref name="textureFor"/> is called once per layer to resolve its spritesheet texture.
    /// </summary>
    public void Render(Rectangle cameraWorldPixels, SpriteBatch spriteBatch,
                       Func<TilemapLayerContext, Texture2D?> textureFor,
                       TilemapClock clock = default)
    {
        foreach (var ls in _layers)
        {
            var drawer = new SpriteBatchTileDrawer(spriteBatch, textureFor(ls.Context));
            RenderLayer(ls, cameraWorldPixels, drawer, clock);
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>Unsubscribes from all <see cref="TilemapData.ChunkDirty"/> events.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (data, handler) in _subscriptions)
            data.ChunkDirty -= handler;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void RenderLayer(LayerState ls, Rectangle camera, ITileDrawer drawer, TilemapClock clock)
    {
        var layer   = ls.Context.Layer;
        var tileset = ls.Context.Tileset;
        int tileW   = tileset.TileSize.X;
        int tileH   = tileset.TileSize.Y;

        // Sheet width: prefer explicit override (test scenarios) over real texture.
        int sheetWidth  = ls.Context.SpritesheetWidth ?? ls.Context.Texture?.Width ?? 0;
        int tilesPerRow = (sheetWidth > 0 && tileW > 0)
                        ? System.Math.Max(1, sheetWidth / tileW)
                        : 1;

        foreach (var coord in layer.OccupiedChunks)
        {
            if (!ChunkIntersectsCamera(coord, tileset.TileSize, camera))
                continue;

            if (!ls.Caches.TryGetValue(coord, out var cache))
            {
                cache = new ChunkCache();
                ls.Caches[coord] = cache;
            }

            if (cache.IsDirty)
                RebuildCache(coord, layer, tileset, cache);

            foreach (var (dest, resolvedId) in cache.Quads)
            {
                // Animation: if the base tile has frames, substitute the current frame's TileId.
                var meta   = FindMeta(tileset, resolvedId);
                TileId drawId;
                if (meta is not null && meta.Frames.Count > 0)
                {
                    int period   = meta.Frames[0].DurationMs;
                    int frameIdx = (period > 0)
                                 ? (int)(clock.ElapsedMs / period % meta.Frames.Count)
                                 : 0;
                    drawId = meta.Frames[frameIdx].TileId;
                }
                else
                {
                    drawId = resolvedId;
                }

                Rectangle src;
                if (sheetWidth > 0 && tileW > 0)
                {
                    int idx = System.Math.Max(0, drawId.Value - 1);
                    src = new Rectangle(
                        (idx % tilesPerRow) * tileW,
                        (idx / tilesPerRow) * tileH,
                        tileW, tileH);
                }
                else
                {
                    src = Rectangle.Empty;
                }

                drawer.Draw(dest, src);
            }
        }
    }

    private void InvalidateChunk(LayerState ls, ChunkCoord coord)
    {
        if (ls.Caches.TryGetValue(coord, out var cache))
            cache.IsDirty = true;
        // Chunks not yet cached are dirty by default; nothing extra to do.
    }

    // Rebuilds the cache for one chunk. Stores (destRect, resolvedTileId) only —
    // source rect computation and animation substitution happen at draw time.
    private void RebuildCache(ChunkCoord coord, TilemapData layer, TilesetData tileset, ChunkCache cache)
    {
        cache.Quads.Clear();
        cache.IsDirty = false;

        int tileW = tileset.TileSize.X;
        int tileH = tileset.TileSize.Y;

        for (int ly = 0; ly < ChunkCoord.ChunkSize; ly++)
        for (int lx = 0; lx < ChunkCoord.ChunkSize; lx++)
        {
            int wx       = coord.X * ChunkCoord.ChunkSize + lx;
            int wy       = coord.Y * ChunkCoord.ChunkSize + ly;
            var worldPos = new Vector2Int(wx, wy);

            var resolved = Autotile.Resolve(layer, worldPos, tileset);
            if (resolved == TileId.Empty) continue;

            cache.Quads.Add((new Rectangle(wx * tileW, wy * tileH, tileW, tileH), resolved));
        }

        ChunkCacheRebuilt?.Invoke(coord);
    }

    private static bool ChunkIntersectsCamera(ChunkCoord coord, Vector2Int tileSize, Rectangle camera)
    {
        var chunkRect = new Rectangle(
            coord.X * ChunkCoord.ChunkSize * tileSize.X,
            coord.Y * ChunkCoord.ChunkSize * tileSize.Y,
            ChunkCoord.ChunkSize * tileSize.X,
            ChunkCoord.ChunkSize * tileSize.Y);
        return camera.Intersects(chunkRect);
    }

    private static TileMetadata? FindMeta(TilesetData tileset, TileId id)
    {
        foreach (var m in tileset.Tiles)
            if (m.Id == id) return m;
        return null;
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private sealed class LayerState
    {
        public TilemapLayerContext Context { get; }
        public Dictionary<ChunkCoord, ChunkCache> Caches { get; } = new();
        public LayerState(TilemapLayerContext ctx) => Context = ctx;
    }

    private sealed class ChunkCache
    {
        public bool IsDirty = true;
        public List<(Rectangle Dest, TileId ResolvedId)> Quads = new();
    }
}
