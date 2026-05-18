using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Tiles;

namespace Engine.Core.Tests.Tiles;

/// <summary>
/// Tests for multi-terrain priority resolution in <see cref="Autotile.Resolve"/>.
/// Priority rule: a neighbour counts as "same terrain" when it shares the terrain tag
/// OR has strictly higher TerrainPriority.
/// </summary>
public class AutotileMultiTerrainTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────────

    private sealed record Terrain(string Tag, int Priority, int IdBase);

    private static readonly Terrain Dirt   = new("dirt",   0,   1); // TileIds   1-47
    private static readonly Terrain Grass  = new("grass",  1,  51); // TileIds  51-97
    private static readonly Terrain Stone  = new("stone",  2, 101); // TileIds 101-147
    private static readonly Terrain Forest = new("forest", 1, 151); // TileIds 151-197

    // TileId for variant V of terrain T.
    private static TileId TileFor(Terrain t, int variant) => new(t.IdBase + variant);

    // Tileset holding 47 blob variants for each of the supplied terrains.
    private static TilesetData BuildTileset(params Terrain[] terrains)
    {
        var ts = new TilesetData { Name = "multi" };
        foreach (var t in terrains)
            for (int v = 0; v < 47; v++)
                ts.Tiles.Add(new TileMetadata
                {
                    Id              = TileFor(t, v),
                    TerrainTag      = t.Tag,
                    TerrainPriority = t.Priority,
                    BlobVariant     = v,
                });
        return ts;
    }

    // ── Grass-over-dirt boundary ──────────────────────────────────────────────
    //
    // Layout: grass(0,0)  dirt(1,0)
    //
    // Grass perspective: east=dirt, priority 0 < grass priority 1 → doesn't count.
    //   mask=0 → variant 0 (isolated) — grass appears as a floating island.
    //
    // Dirt perspective: west=grass, priority 1 > dirt priority 0 → counts!
    //   W bit (bit 6) = 64 → variant 13 — dirt shows a seamless western edge.

    [Fact]
    public void Resolve_GrassTileBesideDirt_ShowsIsolatedVariant()
    {
        var tileset = BuildTileset(Dirt, Grass);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Grass, 0));
        layer.SetTile(new Vector2Int(1, 0), TileFor(Dirt,  0));

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Grass, 0),
            "grass (priority 1) does not blob into lower-priority dirt — mask=0, variant 0");
    }

    [Fact]
    public void Resolve_DirtTileBesideGrass_ShowsFilledEdge()
    {
        var tileset = BuildTileset(Dirt, Grass);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Grass, 0)); // grass to west
        layer.SetTile(new Vector2Int(1, 0), TileFor(Dirt,  0)); // dirt at center

        var result = Autotile.Resolve(layer, new Vector2Int(1, 0), tileset);

        result.Should().Be(TileFor(Dirt, 13),
            "dirt (priority 0) treats higher-priority grass to the west as same — mask=64, variant 13");
    }

    // ── Three-way corner — highest priority dominates ─────────────────────────
    //
    // Dirt(0,0) surrounded by: grass(0,-1) to N, stone(−1,0) to W, stone(−1,−1) to NW.
    //
    // From dirt's perspective (priority 0): grass (1>0) and stone (2>0) both count.
    // N(bit0)+W(bit6)+NW(bit7) = 1+64+128 = 193.
    // Corner rule: NW requires N AND W — both set → NW stays.
    // Mask 193 → variant 34.

    [Fact]
    public void Resolve_ThreeWayCorner_DirtSeesGrassAndStoneSameTerrain()
    {
        var tileset = BuildTileset(Dirt, Grass, Stone);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int( 0,  0), TileFor(Dirt,  0));
        layer.SetTile(new Vector2Int( 0, -1), TileFor(Grass, 0)); // N
        layer.SetTile(new Vector2Int(-1,  0), TileFor(Stone, 0)); // W
        layer.SetTile(new Vector2Int(-1, -1), TileFor(Stone, 0)); // NW

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Dirt, 34),
            "dirt treats both grass (priority 1) and stone (priority 2) as same — mask=193, variant 34");
    }

    [Fact]
    public void Resolve_ThreeWayCorner_GrassSeesOnlyStone()
    {
        // Grass (priority 1) at (0,0): dirt (0) doesn't count, stone (2) does.
        // stone to N only → N bit (bit0) = 1 → variant 1.
        var tileset = BuildTileset(Dirt, Grass, Stone);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int( 0,  0), TileFor(Grass, 0));
        layer.SetTile(new Vector2Int( 0, -1), TileFor(Stone, 0)); // N: counts (2 > 1)
        layer.SetTile(new Vector2Int( 1,  0), TileFor(Dirt,  0)); // E: doesn't count (0 < 1)

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Grass, 1),
            "grass (priority 1) only counts stone (priority 2) to N — mask=1, variant 1");
    }

    [Fact]
    public void Resolve_ThreeWayCorner_StoneIsIsolated()
    {
        // Stone (priority 2) at the corner: nothing can have higher priority.
        // No neighbours count → mask=0 → variant 0 (isolated).
        var tileset = BuildTileset(Dirt, Grass, Stone);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Stone, 0));
        layer.SetTile(new Vector2Int(1, 0), TileFor(Grass, 0)); // E (priority 1 < 2)
        layer.SetTile(new Vector2Int(0, 1), TileFor(Dirt,  0)); // S (priority 0 < 2)

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Stone, 0),
            "highest-priority stone has no higher-priority neighbours — mask=0, variant 0");
    }

    // ── Same-priority different terrain — no blobbing ─────────────────────────
    //
    // Grass (priority 1) and Forest (priority 1): equal priority, different tags.
    // Neither satisfies priority > self, and tags don't match → both isolated.

    [Fact]
    public void Resolve_SamePriorityDifferentTerrain_GrassShowsIsolated()
    {
        var tileset = BuildTileset(Grass, Forest);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Grass,  0));
        layer.SetTile(new Vector2Int(1, 0), TileFor(Forest, 0)); // E: equal priority, different tag

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Grass, 0),
            "grass does not blob into equal-priority forest — mask=0, variant 0");
    }

    [Fact]
    public void Resolve_SamePriorityDifferentTerrain_ForestShowsIsolated()
    {
        var tileset = BuildTileset(Grass, Forest);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Grass,  0)); // W: equal priority, different tag
        layer.SetTile(new Vector2Int(1, 0), TileFor(Forest, 0));

        var result = Autotile.Resolve(layer, new Vector2Int(1, 0), tileset);

        result.Should().Be(TileFor(Forest, 0),
            "forest does not blob into equal-priority grass — mask=0, variant 0");
    }

    // ── Same terrain still blobs regardless of priority ───────────────────────

    [Fact]
    public void Resolve_SameTerrainTag_StillBlobs()
    {
        // Two grass tiles with the same tag blob normally via the tag-match path.
        // Grass at (0,0) with grass to east: E bit (bit2) = 4 → variant 2.
        var tileset = BuildTileset(Grass);
        var layer   = new TilemapData();
        layer.SetTile(new Vector2Int(0, 0), TileFor(Grass, 0));
        layer.SetTile(new Vector2Int(1, 0), TileFor(Grass, 0));

        var result = Autotile.Resolve(layer, new Vector2Int(0, 0), tileset);

        result.Should().Be(TileFor(Grass, 2),
            "same-tag grass tiles blob with each other — mask=4, variant 2");
    }
}
