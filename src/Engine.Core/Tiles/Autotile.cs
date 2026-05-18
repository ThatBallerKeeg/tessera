using Engine.Core.Math;

namespace Engine.Core.Tiles;

/// <summary>
/// 47-blob autotile resolver (Blackman &amp; Vigna corner-aware variant).
/// <para>
/// The 8-neighbour bitmask uses this bit layout (N=0 clockwise to NW=7):
/// <code>
///   NW(7) N(0) NE(1)
///   W(6)  [·]  E(2)
///   SW(5) S(4) SE(3)
/// </code>
/// Diagonal bits are counted only when <em>both</em> adjacent cardinal bits are also set
/// (corner-aware rule). This collapses 256 raw masks to 47 valid blob variants.
/// </para>
/// </summary>
public static class Autotile
{
    // ── Bit indices ───────────────────────────────────────────────────────────
    private const int N = 0, NE = 1, E = 2, SE = 3;
    private const int S = 4, SW = 5, W = 6, NW = 7;

    // Direction offsets indexed by bit position (N, NE, E, SE, S, SW, W, NW).
    private static readonly Vector2Int[] Offsets =
    [
        new( 0, -1),  // N
        new( 1, -1),  // NE
        new( 1,  0),  // E
        new( 1,  1),  // SE
        new( 0,  1),  // S
        new(-1,  1),  // SW
        new(-1,  0),  // W
        new(-1, -1),  // NW
    ];

    // The 47 valid blob masks in ascending order; array index == variant index.
    // Derived by exhaustively applying the corner rule to all 256 raw masks.
    private static readonly int[] ValidMasks =
    [
          0,   1,   4,   5,   7,  16,  17,  20,  21,  23,
         28,  29,  31,  64,  65,  68,  69,  71,  80,  81,
         84,  85,  87,  92,  93,  95, 112, 113, 116, 117,
        119, 124, 125, 127, 193, 197, 199, 209, 213, 215,
        221, 223, 241, 245, 247, 253, 255,
    ];

    // 256-element map: raw (corner-corrected) mask → variant index 0–46, or -1.
    private static readonly int[] VariantTable = BuildVariantTable();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the autotile variant that should be displayed at
    /// <paramref name="position"/> given its same-terrain neighbours.
    /// </summary>
    /// <returns>
    /// The <see cref="TileId"/> of the matching blob variant in
    /// <paramref name="tileset"/>, or <see cref="TileId.Empty"/> if the
    /// position is empty, or the base tile unchanged if it is not an autotile
    /// (no terrain tag, or <see cref="TileMetadata.BlobVariant"/> &lt; 0).
    /// </returns>
    public static TileId Resolve(TilemapData layer, Vector2Int position, TilesetData tileset)
    {
        var baseTile = layer.GetTile(position);
        if (baseTile == TileId.Empty) return TileId.Empty;

        var baseMeta = FindMeta(tileset, baseTile);
        if (baseMeta is null || baseMeta.TerrainTag.Length == 0 || baseMeta.BlobVariant < 0)
            return baseTile;

        string tag      = baseMeta.TerrainTag;
        int    rawMask  = BuildNeighborMask(layer, position, tag, tileset);
        int    blobMask = ApplyCornerRule(rawMask);
        int    variant  = VariantTable[blobMask];

        // Find the tile in the tileset that carries this variant for the same terrain.
        foreach (var meta in tileset.Tiles)
        {
            if (meta.TerrainTag == tag && meta.BlobVariant == variant)
                return meta.Id;
        }
        return baseTile;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static int BuildNeighborMask(
        TilemapData layer, Vector2Int pos, string tag, TilesetData tileset)
    {
        int mask = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            var neighbor = pos + Offsets[bit];
            if (IsSameTerrain(layer.GetTile(neighbor), tag, tileset))
                mask |= 1 << bit;
        }
        return mask;
    }

    private static bool IsSameTerrain(TileId tile, string tag, TilesetData tileset)
    {
        if (tile == TileId.Empty) return false;
        var meta = FindMeta(tileset, tile);
        return meta is not null && meta.TerrainTag == tag;
    }

    // Clears diagonal bits whose adjacent cardinal pair is incomplete.
    private static int ApplyCornerRule(int raw)
    {
        int m = raw;
        if ((m & ((1 << N) | (1 << E))) != ((1 << N) | (1 << E))) m &= ~(1 << NE);
        if ((m & ((1 << E) | (1 << S))) != ((1 << E) | (1 << S))) m &= ~(1 << SE);
        if ((m & ((1 << S) | (1 << W))) != ((1 << S) | (1 << W))) m &= ~(1 << SW);
        if ((m & ((1 << W) | (1 << N))) != ((1 << W) | (1 << N))) m &= ~(1 << NW);
        return m;
    }

    private static TileMetadata? FindMeta(TilesetData tileset, TileId id)
    {
        foreach (var m in tileset.Tiles)
            if (m.Id == id) return m;
        return null;
    }

    private static int[] BuildVariantTable()
    {
        var table = new int[256];
        for (int i = 0; i < 256; i++) table[i] = -1;
        for (int v = 0; v < ValidMasks.Length; v++)
            table[ValidMasks[v]] = v;
        return table;
    }
}
