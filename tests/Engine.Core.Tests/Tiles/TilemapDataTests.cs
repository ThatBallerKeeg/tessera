using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Engine.Core.Tiles;

namespace Engine.Core.Tests.Tiles;

public class TilemapDataTests
{
    private static SceneData SaveAndLoad(SceneData scene)
    {
        using var stream = new MemoryStream();
        SceneSerializer.Save(scene, stream);
        stream.Position = 0;
        return SceneSerializer.Load(stream);
    }

    [Fact]
    public void RoundTrip_EmptyTilemapData_PreservesMetadata()
    {
        var tilemap = new TilemapData
        {
            LayerName  = "floor",
            LayerIndex = 0,
            TilesetRef = new Guid("aaaabbbb-cccc-dddd-eeee-ffffffffffff"),
        };
        var scene = new SceneData { Tilemaps = [tilemap] };

        var loaded = SaveAndLoad(scene).Tilemaps[0];

        loaded.LayerName.Should().Be("floor");
        loaded.LayerIndex.Should().Be(0);
        loaded.TilesetRef.Should().Be(tilemap.TilesetRef);
        loaded.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_TilemapWithTiles_PreservesAllPositionsAndIds()
    {
        var tilemap = new TilemapData { LayerName = "walls" };

        // ~50 tiles across a few different positions (including negative coords)
        var expected = new List<(Vector2Int pos, TileId id)>();
        for (int i = 0; i < 50; i++)
        {
            var pos = new Vector2Int(i * 3, i - 10);   // spans positive and negative y
            var id  = new TileId(i + 1);                // non-zero
            tilemap.SetTile(pos, id);
            expected.Add((pos, id));
        }

        var scene  = new SceneData { Tilemaps = [tilemap] };
        var loaded = SaveAndLoad(scene).Tilemaps[0];

        foreach (var (pos, id) in expected)
            loaded.GetTile(pos).Should().Be(id, $"tile at {pos} should survive round-trip");
    }

    [Fact]
    public void BulkEdit_FiresChunkDirtyOncePerChunk_NotPerTile()
    {
        var tilemap = new TilemapData();
        var firedCoords = new List<ChunkCoord>();
        tilemap.ChunkDirty += c => firedCoords.Add(c);

        // Write tiles covering exactly 8 distinct chunks: (0,0)…(7,0)
        // 8 chunks × 16 columns × 8 rows each = 1 024 SetTile calls
        using (tilemap.BeginBulkEdit())
        {
            for (int cx = 0; cx < 8; cx++)
            for (int ly = 0; ly < 8; ly++)
            for (int lx = 0; lx < 16; lx++)
                tilemap.SetTile(new Vector2Int(cx * 16 + lx, ly), new TileId(1));
        }

        firedCoords.Should().HaveCount(8, "one event per affected chunk, not one per tile");
        firedCoords.Distinct().Should().HaveCount(8, "each chunk coord fired exactly once");
    }

    [Fact]
    public void SetTile_EraseLastTileInChunk_PrunesChunk()
    {
        var tilemap = new TilemapData();
        var pos     = new Vector2Int(5, 5);

        tilemap.SetTile(pos, new TileId(42));
        tilemap.Chunks.Should().NotBeEmpty("chunk should be created when a tile is set");

        tilemap.SetTile(pos, TileId.Empty);
        tilemap.Chunks.Should().BeEmpty("empty chunk should be pruned");
    }
}
