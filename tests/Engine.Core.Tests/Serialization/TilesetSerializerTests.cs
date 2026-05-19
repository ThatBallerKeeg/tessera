using System.Text.Json;
using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Serialization;
using Engine.Core.Tiles;

namespace Engine.Core.Tests.Serialization;

public class TilesetSerializerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"ts_{Guid.NewGuid():N}.tileset.json");

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveLoad_RoundTrip_PreservesAllMetadataFields()
    {
        // 2-column tileset: TileId(1) = (0,0), TileId(2) = (1,0), TileId(3) = (0,1), TileId(4) = (1,1)
        const int cols = 2;
        var tileset = new TilesetData
        {
            Name      = "grass",
            ImagePath = "/project/Assets/grass.png",
            TileSize  = new Vector2Int(16, 16),
            Tiles     =
            [
                new TileMetadata { Id = new TileId(1), Solid = false, TerrainTag = "grass", TerrainPriority = 1, BlobVariant =  0 },
                new TileMetadata { Id = new TileId(2), Solid = false, TerrainTag = "grass", TerrainPriority = 1, BlobVariant =  1 },
                new TileMetadata { Id = new TileId(3), Solid = true,  TerrainTag = "rock",  TerrainPriority = 2, BlobVariant = -1 },
                new TileMetadata { Id = new TileId(4), Solid = false, TerrainTag = "",      TerrainPriority = 0, BlobVariant = -1 },
            ],
        };

        var path = TempPath();
        try
        {
            TilesetSerializer.Save(tileset, path, cols);

            // imageWidth = cols * tileWidth = 2 * 16 = 32
            var loaded = TilesetSerializer.Load(path, "grass", "/project/Assets/grass.png",
                imageWidth: 32, imageHeight: 32);

            loaded.Name.Should().Be("grass");
            loaded.TileSize.X.Should().Be(16);
            loaded.TileSize.Y.Should().Be(16);
            loaded.Tiles.Should().HaveCount(4);

            loaded.Tiles[0].Id.Value.Should().Be(1);
            loaded.Tiles[0].TerrainTag.Should().Be("grass");
            loaded.Tiles[0].TerrainPriority.Should().Be(1);
            loaded.Tiles[0].BlobVariant.Should().Be(0);
            loaded.Tiles[0].Solid.Should().BeFalse();

            loaded.Tiles[2].Id.Value.Should().Be(3);
            loaded.Tiles[2].Solid.Should().BeTrue();
            loaded.Tiles[2].TerrainTag.Should().Be("rock");
            loaded.Tiles[2].TerrainPriority.Should().Be(2);
            loaded.Tiles[2].BlobVariant.Should().Be(-1);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // ── JSON shape ────────────────────────────────────────────────────────────

    [Fact]
    public void Save_ProducesDocumentedJsonShape()
    {
        var tileset = new TilesetData
        {
            Name     = "grass",
            TileSize = new Vector2Int(16, 16),
            Tiles    =
            [
                new TileMetadata
                {
                    Id = new TileId(1), Solid = false,
                    TerrainTag = "grass", TerrainPriority = 1, BlobVariant = 0,
                },
            ],
        };

        var path = TempPath();
        try
        {
            TilesetSerializer.Save(tileset, path, imageCols: 4);

            using var doc  = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            // tileSize is a two-element JSON array [w, h]
            root.TryGetProperty("tileSize", out var tileSize).Should().BeTrue();
            tileSize.ValueKind.Should().Be(JsonValueKind.Array);
            tileSize.GetArrayLength().Should().Be(2);
            tileSize[0].GetInt32().Should().Be(16);
            tileSize[1].GetInt32().Should().Be(16);

            // tiles is an array of objects with the documented six fields
            root.TryGetProperty("tiles", out var tiles).Should().BeTrue();
            tiles.ValueKind.Should().Be(JsonValueKind.Array);
            tiles.GetArrayLength().Should().Be(1);

            var tile = tiles[0];
            tile.TryGetProperty("x",               out var xProp).Should().BeTrue();
            tile.TryGetProperty("y",               out var yProp).Should().BeTrue();
            tile.TryGetProperty("solid",           out _).Should().BeTrue();
            tile.TryGetProperty("terrainTag",      out _).Should().BeTrue();
            tile.TryGetProperty("terrainPriority", out _).Should().BeTrue();
            tile.TryGetProperty("blobVariant",     out _).Should().BeTrue();

            // TileId(1) with 4 cols → x=0, y=0
            xProp.GetInt32().Should().Be(0);
            yProp.GetInt32().Should().Be(0);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // ── Generate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_CreatesOneMetadataEntryPerTileSlot()
    {
        // 32×16 image, 16×16 tiles → 2 cols, 1 row → 2 tiles
        var tileset = TilesetSerializer.Generate(
            "grass", "/path/grass.png",
            imageWidth: 32, imageHeight: 16,
            tileSize: new Vector2Int(16, 16));

        tileset.Name.Should().Be("grass");
        tileset.TileSize.X.Should().Be(16);
        tileset.TileSize.Y.Should().Be(16);
        tileset.Tiles.Should().HaveCount(2);
        tileset.Tiles[0].Id.Value.Should().Be(1);
        tileset.Tiles[1].Id.Value.Should().Be(2);
        tileset.Tiles[0].BlobVariant.Should().Be(-1); // default
    }

    [Fact]
    public void Generate_ThreeByTwoGrid_CreatesCorrectTileIds()
    {
        // 48×32 image, 16×16 tiles → 3 cols, 2 rows → 6 tiles
        var tileset = TilesetSerializer.Generate(
            "sheet", "/path/sheet.png",
            imageWidth: 48, imageHeight: 32,
            tileSize: new Vector2Int(16, 16));

        tileset.Tiles.Should().HaveCount(6);
        tileset.Tiles[0].Id.Value.Should().Be(1); // row 0, col 0
        tileset.Tiles[2].Id.Value.Should().Be(3); // row 0, col 2
        tileset.Tiles[3].Id.Value.Should().Be(4); // row 1, col 0
        tileset.Tiles[5].Id.Value.Should().Be(6); // row 1, col 2
    }

    // ── FindSidecar ───────────────────────────────────────────────────────────

    [Fact]
    public void FindSidecar_ReturnsSiblingPath_WhenFileExists()
    {
        var pngPath     = TempPath().Replace(".tileset.json", ".png");
        var sidecarPath = Path.Combine(
            Path.GetDirectoryName(pngPath)!,
            Path.GetFileNameWithoutExtension(pngPath) + ".tileset.json");

        try
        {
            File.WriteAllText(sidecarPath, "{}");
            TilesetSerializer.FindSidecar(pngPath).Should().Be(sidecarPath);
        }
        finally { if (File.Exists(sidecarPath)) File.Delete(sidecarPath); }
    }

    [Fact]
    public void FindSidecar_ReturnsNull_WhenNoSidecarExists()
    {
        TilesetSerializer.FindSidecar("/nonexistent/path/grass.png").Should().BeNull();
    }
}
