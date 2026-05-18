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

    // ── Animated tile support ─────────────────────────────────────────────────
    //
    // Tileset: TileId(1) is a 6-frame animated tile, each frame 200ms.
    // Frame f uses TileId(f+1), laid out left-to-right on a 96×16 px spritesheet.
    // Source rect X for frame f = f * 16.
    //
    // SpritesheetWidth=96 lets the renderer compute source rects without a real Texture2D.

    // Builds a tileset with one animated tile: TileId(1) with 6 × 200ms frames
    // mapping to TileId(1)..TileId(6), each 16px wide on a 96×16 spritesheet.
    private static TilesetData BuildAnimatedTileset()
    {
        var ts = new TilesetData { Name = "animated", TileSize = new Vector2Int(16, 16) };
        var meta = new TileMetadata { Id = new TileId(1), BlobVariant = -1 };
        for (int f = 0; f < 6; f++)
            meta.Frames.Add(new TileFrame { TileId = new TileId(f + 1), DurationMs = 200 });
        ts.Tiles.Add(meta);
        return ts;
    }

    // Records source rectangles for each Draw call.
    private sealed class RecordingDrawer : ITileDrawer
    {
        public List<Rectangle> Sources { get; } = new();
        public void Draw(Rectangle destinationRect, Rectangle sourceRect)
            => Sources.Add(sourceRect);
    }

    [Fact]
    public void Render_AnimatedTile_CyclesCorrectlyAtKnownClockStates()
    {
        // Spritesheet: 6 tiles wide (96px), each 16px.
        // Frame f → TileId(f+1) → sprite index f → src.X = f * 16.
        var tileset = BuildAnimatedTileset();
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), new TileId(1));

        var ctx = new TilemapLayerContext
        {
            Layer            = layer,
            Tileset          = tileset,
            SpritesheetWidth = 96,   // 6 tiles × 16px
        };
        using var renderer = new TilemapRenderer([ctx]);
        var bigCamera = new Rectangle(0, 0, 10000, 10000);

        void AssertFrameX(long elapsedMs, int expectedSrcX, string reason)
        {
            var drawer = new RecordingDrawer();
            renderer.Render(bigCamera, drawer, new TilemapClock(elapsedMs));
            drawer.Sources.Should().HaveCount(1);
            drawer.Sources[0].X.Should().Be(expectedSrcX, reason);
        }

        AssertFrameX(   0, 0,  "t=0ms → frame 0, src.X=0");
        AssertFrameX( 200, 16, "t=200ms → frame 1, src.X=16");
        AssertFrameX(1000, 80, "t=1000ms → frame 5, src.X=80");
        AssertFrameX(1200, 0,  "t=1200ms → wraps to frame 0 (6-frame × 200ms = 1200ms cycle)");
    }

    [Fact]
    public void Render_AnimatedTile_MultipleInstancesInSync()
    {
        // Two animated tiles at different positions must show the same frame at the same clock.
        var tileset = BuildAnimatedTileset();
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0,  0), new TileId(1));
        layer.SetTile(new Vector2Int(10, 0), new TileId(1));  // different position, same tile

        var ctx = new TilemapLayerContext
        {
            Layer            = layer,
            Tileset          = tileset,
            SpritesheetWidth = 96,
        };
        using var renderer = new TilemapRenderer([ctx]);

        // t=400ms → frameIdx = 400/200 % 6 = 2 → TileId(3) → idx=2 → src.X=32
        var drawer = new RecordingDrawer();
        renderer.Render(new Rectangle(0, 0, 10000, 10000), drawer, new TilemapClock(400));

        drawer.Sources.Should().HaveCount(2, "two tiles in the layer");
        foreach (var src in drawer.Sources)
            src.X.Should().Be(32, "both instances must be at frame 2 (src.X=32)");
    }

    [Fact]
    public void Render_StaticTile_SourceRectUnchangedAcrossClockValues()
    {
        // A tile with no Frames must produce an identical source rect regardless of the clock.
        var tileset = BuildTileset();  // BlobVariant=-1, no Frames
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), new TileId(1));

        var ctx = new TilemapLayerContext
        {
            Layer            = layer,
            Tileset          = tileset,
            SpritesheetWidth = 16,   // 1 tile wide → TileId(1) → src=(0,0,16,16)
        };
        using var renderer = new TilemapRenderer([ctx]);
        var bigCamera = new Rectangle(0, 0, 10000, 10000);

        var d1 = new RecordingDrawer();
        renderer.Render(bigCamera, d1, new TilemapClock(0));

        var d2 = new RecordingDrawer();
        renderer.Render(bigCamera, d2, new TilemapClock(99_999));

        d1.Sources[0].Should().Be(d2.Sources[0],
            "static tile source rect must not change with the clock");
    }
}
