using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Tiles;
using Engine.Runtime.Tiles;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Engine.Runtime.Tests.Tiles;

public class TilemapRendererTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────────

    // 16×16 px tiles; one static (non-autotile) tile with Id=1.
    private static TilesetData BuildTileset() => new()
    {
        Name     = "test",
        TileSize = new Vector2Int(16, 16),
        Tiles    = { new TileMetadata { Id = new TileId(1), BlobVariant = -1 } },
    };

    // Counting implementation of ITileDrawer — no real GPU needed.
    private sealed class DrawCallCounter : ITileDrawer
    {
        public int Count { get; private set; }
        public void Draw(Rectangle destinationRect, Rectangle sourceRect) => Count++;
    }

    // ── Zero chunks → zero draw calls ────────────────────────────────────────

    [Fact]
    public void Render_NoChunks_ZeroDrawCalls()
    {
        var layer   = new TilemapData();
        var tileset = BuildTileset();
        var ctx     = new TilemapLayerContext { Layer = layer, Tileset = tileset };
        using var renderer = new TilemapRenderer([ctx]);

        var counter = new DrawCallCounter();
        renderer.Render(new Rectangle(0, 0, 10000, 10000), counter);

        counter.Count.Should().Be(0, "empty layer has no tiles to draw");
    }

    // ── Frustum culling: 4 visible of 100 ────────────────────────────────────
    //
    // Tile size = 16×16, chunk size = 16 tiles → each chunk is 256×256 px.
    // Chunks are placed at (cx, cy) for cx=0..9, cy=0..9 (100 total).
    // Camera covers pixel rect [0, 1024) × [0, 256) → chunks (0,0),(1,0),(2,0),(3,0).
    // Each chunk has exactly 1 tile → 4 draw calls.

    [Fact]
    public void Render_FourOfHundredChunksVisible_OnlyFourDrawCalls()
    {
        var layer   = new TilemapData();
        var tileset = BuildTileset();

        for (int cy = 0; cy < 10; cy++)
        for (int cx = 0; cx < 10; cx++)
            layer.SetTile(new Vector2Int(cx * ChunkCoord.ChunkSize, cy * ChunkCoord.ChunkSize),
                          new TileId(1));

        var ctx = new TilemapLayerContext { Layer = layer, Tileset = tileset };
        using var renderer = new TilemapRenderer([ctx]);

        // Camera covers exactly the first 4 chunks in row 0.
        int chunkPx = ChunkCoord.ChunkSize * tileset.TileSize.X; // 256
        var camera  = new Rectangle(0, 0, chunkPx * 4, chunkPx);

        var counter = new DrawCallCounter();
        renderer.Render(camera, counter);

        counter.Count.Should().Be(4,
            "only 4 chunks intersect the camera; each has 1 tile → 4 draw calls");
    }

    // ── Cache invalidation: SetTile dirties only the affected chunk ───────────

    [Fact]
    public void Render_SetTile_RebuildsOnlyDirtyChunk()
    {
        var layer   = new TilemapData();
        var tileset = BuildTileset();

        // Two tiles in two distinct chunks.
        layer.SetTile(new Vector2Int(0,  0), new TileId(1));  // chunk (0,0)
        layer.SetTile(new Vector2Int(16, 0), new TileId(1));  // chunk (1,0)

        var ctx = new TilemapLayerContext { Layer = layer, Tileset = tileset };
        using var renderer = new TilemapRenderer([ctx]);

        var counter = new DrawCallCounter();
        var bigCamera = new Rectangle(0, 0, 10000, 10000);

        // Warm up: both chunks build their caches.
        renderer.Render(bigCamera, counter);

        // Subscribe to rebuild events AFTER the warm-up render.
        var rebuiltChunks = new List<ChunkCoord>();
        renderer.ChunkCacheRebuilt += coord => rebuiltChunks.Add(coord);

        // Modify a tile in chunk (0,0) only.
        layer.SetTile(new Vector2Int(1, 0), new TileId(1));

        // Second render: only chunk (0,0) should be dirty and rebuilt.
        renderer.Render(bigCamera, counter);

        rebuiltChunks.Should().HaveCount(1, "exactly one chunk was dirtied");
        rebuiltChunks[0].Should().Be(new ChunkCoord(0, 0),
            "chunk (0,0) was modified; chunk (1,0) was not touched");
    }
}
