using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Tiles;

namespace Engine.Core.Tests.Tiles;

public class AutotileTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────────

    private const string Tag = "grass";

    /// <summary>
    /// Builds a tileset with exactly 47 grass tiles, one per blob variant.
    /// TileId value = variant + 1  (so TileId(1) == variant 0, TileId(47) == variant 46).
    /// </summary>
    private static TilesetData BuildTileset()
    {
        var ts = new TilesetData { Name = "test" };
        for (int v = 0; v < 47; v++)
            ts.Tiles.Add(new TileMetadata
            {
                Id          = new TileId(v + 1),
                TerrainTag  = Tag,
                BlobVariant = v,
            });
        return ts;
    }

    /// <summary>
    /// Places a grass tile at the centre (0,0) and at each neighbour whose
    /// corresponding bit in <paramref name="neighborMask"/> is set.
    /// Bit layout: N=0 NE=1 E=2 SE=3 S=4 SW=5 W=6 NW=7.
    /// </summary>
    private static TilemapData BuildLayer(int neighborMask, TilesetData tileset)
    {
        // Use tile with BlobVariant 0 (= TileId 1) as the canonical "grass" centre tile.
        var grassTile = tileset.Tiles[0].Id;

        var layer  = new TilemapData();
        var center = new Vector2Int(0, 0);
        layer.SetTile(center, grassTile);

        var offsets = new Vector2Int[]
        {
            new( 0, -1),  // bit 0: N
            new( 1, -1),  // bit 1: NE
            new( 1,  0),  // bit 2: E
            new( 1,  1),  // bit 3: SE
            new( 0,  1),  // bit 4: S
            new(-1,  1),  // bit 5: SW
            new(-1,  0),  // bit 6: W
            new(-1, -1),  // bit 7: NW
        };
        for (int bit = 0; bit < 8; bit++)
        {
            if ((neighborMask & (1 << bit)) != 0)
                layer.SetTile(center + offsets[bit], grassTile);
        }
        return layer;
    }

    // ── 47-variant parametrised test (load-bearing) ───────────────────────────

    // The 47 valid blob masks in ascending order; array index == variant index.
    // Derived by exhaustively applying the corner rule to all 256 raw masks.
    // N=bit0 NE=bit1 E=bit2 SE=bit3 S=bit4 SW=bit5 W=bit6 NW=bit7.
    private static readonly int[] ValidMasks =
    [
          0,   1,   4,   5,   7,  16,  17,  20,  21,  23,
         28,  29,  31,  64,  65,  68,  69,  71,  80,  81,
         84,  85,  87,  92,  93,  95, 112, 113, 116, 117,
        119, 124, 125, 127, 193, 197, 199, 209, 213, 215,
        221, 223, 241, 245, 247, 253, 255,
    ];

    public static IEnumerable<object[]> AllVariantCases =>
        ValidMasks.Select((mask, variant) => new object[] { mask, variant });

    /// <summary>
    /// For every one of the 47 valid blob configurations, Resolve must return
    /// the tile whose BlobVariant matches the expected variant index.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllVariantCases))]
    public void Resolve_SingleTerrain_AllValidMasks_CorrectVariant(int mask, int expectedVariant)
    {
        var tileset = BuildTileset();
        var layer   = BuildLayer(mask, tileset);

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        // TileId value = variant + 1 by construction.
        result.Value.Should().Be(expectedVariant + 1,
            $"neighbor mask 0b{Convert.ToString(mask, 2).PadLeft(8, '0')} " +
            $"should resolve to variant {expectedVariant}");
    }

    // ── Empty-position guard ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_EmptyPosition_ReturnsTileIdEmpty()
    {
        var result = Autotile.Resolve(new TilemapData(), new Vector2Int(5, 5), new TilesetData());
        result.Should().Be(TileId.Empty);
    }

    // ── Corner-awareness ──────────────────────────────────────────────────────

    [Fact]
    public void Resolve_DiagonalWithoutBothCardinals_DiagonalIgnored()
    {
        // Place N and NE neighbours but leave E empty.
        // Raw mask = N|NE = 0b00000011 = 3.
        // Corner rule: NE requires N AND E; E is absent → NE cleared → mask = 1 (N only).
        // Expected variant = index of mask 1 = 1 → TileId(2).
        var tileset = BuildTileset();
        var layer   = new TilemapData();
        var center  = new Vector2Int(0, 0);
        var tile    = tileset.Tiles[0].Id;

        layer.SetTile(center,               tile);  // centre
        layer.SetTile(new Vector2Int(0,-1), tile);  // N
        layer.SetTile(new Vector2Int(1,-1), tile);  // NE (should be ignored)
        // E deliberately absent

        var result = Autotile.Resolve(layer, center, tileset);
        result.Value.Should().Be(2, "NE without E support collapses to N-only (variant 1 → TileId 2)");
    }

    [Fact]
    public void Resolve_AllEightNeighborsSameTerrain_ReturnsVariant46()
    {
        // Mask 255 = all neighbours, all corners valid (both adjacent cardinals set).
        // After corner rule: still 255. Variant = 46 (last in sorted table) → TileId(47).
        var tileset = BuildTileset();
        var layer   = BuildLayer(0b11111111, tileset);

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);
        result.Value.Should().Be(47, "fully surrounded tile is variant 46");
    }

    // ── Non-autotile passthrough ──────────────────────────────────────────────

    [Fact]
    public void Resolve_TileWithNoBlobVariant_ReturnsBaseTile()
    {
        // A tile with BlobVariant = -1 is not an autotile; Resolve returns it unchanged.
        var tileset = new TilesetData();
        tileset.Tiles.Add(new TileMetadata
        {
            Id          = new TileId(99),
            TerrainTag  = "rock",
            BlobVariant = -1,
        });

        var layer = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), new TileId(99));

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);
        result.Value.Should().Be(99, "non-autotile tiles are returned as-is");
    }

    [Fact]
    public void Resolve_TileWithNoTerrainTag_ReturnsBaseTile()
    {
        var tileset = new TilesetData();
        tileset.Tiles.Add(new TileMetadata
        {
            Id          = new TileId(7),
            TerrainTag  = "",
            BlobVariant = 0,
        });

        var layer = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), new TileId(7));

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);
        result.Value.Should().Be(7, "tile without terrain tag is returned as-is");
    }
}
