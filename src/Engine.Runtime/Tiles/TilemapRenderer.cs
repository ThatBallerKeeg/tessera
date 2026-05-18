using Engine.Core.Math;
using Engine.Core.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Engine.Runtime.Tiles;

/// <summary>
/// Chunked, frustum-culled tilemap renderer.
/// <para>
/// Each chunk maintains a render cache (precomputed list of destination/source rect pairs).
/// The cache is built lazily on first render and invalidated whenever <see cref="TilemapData.ChunkDirty"/>
/// fires. <see cref="Engine.Core.Tiles.Autotile.Resolve"/> is called per tile during cache builds.
/// </para>
/// <para>
/// Layers are drawn in ascending <see cref="TilemapData.LayerIndex"/> order.
/// Chunks whose pixel extents do not intersect the camera rectangle are skipped entirely.
/// </para>
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
    /// Renders all layers to <paramref name="drawer"/>, culling chunks outside
    /// <paramref name="cameraWorldPixels"/>.
    /// </summary>
    public void Render(Rectangle cameraWorldPixels, ITileDrawer drawer)
    {
        foreach (var ls in _layers)
            RenderLayer(ls, cameraWorldPixels, drawer);
    }

    /// <summary>
    /// Renders all layers to <paramref name="spriteBatch"/>.
    /// <paramref name="textureFor"/> is called once per layer to resolve its spritesheet texture.
    /// </summary>
    public void Render(Rectangle cameraWorldPixels, SpriteBatch spriteBatch,
                       Func<TilemapLayerContext, Texture2D?> textureFor)
    {
        foreach (var ls in _layers)
        {
            var drawer = new SpriteBatchTileDrawer(spriteBatch, textureFor(ls.Context));
            RenderLayer(ls, cameraWorldPixels, drawer);
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

    private void RenderLayer(LayerState ls, Rectangle camera, ITileDrawer drawer)
    {
        var layer   = ls.Context.Layer;
        var tileset = ls.Context.Tileset;

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
                RebuildCache(coord, layer, tileset, ls.Context.Texture, cache);

            foreach (var (dest, src) in cache.Quads)
                drawer.Draw(dest, src);
        }
    }

    private void InvalidateChunk(LayerState ls, ChunkCoord coord)
    {
        if (ls.Caches.TryGetValue(coord, out var cache))
            cache.IsDirty = true;
        // Chunks not yet cached are dirty by default; nothing extra to do.
    }

    private void RebuildCache(ChunkCoord coord, TilemapData layer, TilesetData tileset,
                               Texture2D? texture, ChunkCache cache)
    {
        cache.Quads.Clear();
        cache.IsDirty = false;

        int tileW       = tileset.TileSize.X;
        int tileH       = tileset.TileSize.Y;
        int tilesPerRow = (texture is null || tileW == 0)
                        ? 1
                        : System.Math.Max(1, texture.Width / tileW);

        for (int ly = 0; ly < ChunkCoord.ChunkSize; ly++)
        for (int lx = 0; lx < ChunkCoord.ChunkSize; lx++)
        {
            int wx       = coord.X * ChunkCoord.ChunkSize + lx;
            int wy       = coord.Y * ChunkCoord.ChunkSize + ly;
            var worldPos = new Vector2Int(wx, wy);

            var resolved = Autotile.Resolve(layer, worldPos, tileset);
            if (resolved == TileId.Empty) continue;

            var dest = new Rectangle(wx * tileW, wy * tileH, tileW, tileH);

            Rectangle src;
            if (texture is null)
            {
                src = Rectangle.Empty;
            }
            else
            {
                int idx = System.Math.Max(0, resolved.Value - 1);
                src = new Rectangle(
                    (idx % tilesPerRow) * tileW,
                    (idx / tilesPerRow) * tileH,
                    tileW, tileH);
            }

            cache.Quads.Add((dest, src));
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
        public List<(Rectangle Dest, Rectangle Src)> Quads = new();
    }
}
