using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Math;
using Engine.Core.Tiles;

namespace Engine.Core.Serialization;

/// <summary>
/// Serialises and deserialises <see cref="TilesetData"/> to/from the sidecar
/// <c>.tileset.json</c> format documented in phase-1-tilemap.md.
/// <para>
/// Wire format:
/// <code>
/// {
///   "tileSize": [16, 16],
///   "tiles": [
///     { "x": 0, "y": 0, "solid": false, "terrainTag": "grass",
///       "terrainPriority": 1, "blobVariant": 0 }
///   ]
/// }
/// </code>
/// </para>
/// </summary>
public static class TilesetSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented            = true,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // ── Sidecar discovery ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the sibling sidecar path for <paramref name="imagePath"/> if it exists,
    /// e.g. <c>grass.png</c> → <c>grass.tileset.json</c>.
    /// Returns null when no sidecar is found.
    /// </summary>
    public static string? FindSidecar(string imagePath)
    {
        string sidecar = Path.Combine(
            Path.GetDirectoryName(imagePath) ?? ".",
            Path.GetFileNameWithoutExtension(imagePath) + ".tileset.json");
        return File.Exists(sidecar) ? sidecar : null;
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a <see cref="TilesetData"/> from a sidecar <c>.tileset.json</c> file.
    /// TileIds are derived from the (x, y) grid coordinates using
    /// <paramref name="imageWidth"/> and the sidecar's tileSize.
    /// </summary>
    public static TilesetData Load(
        string sidecarPath, string name, string imagePath,
        int imageWidth, int imageHeight)
    {
        string json = File.ReadAllText(sidecarPath);
        var root = JsonSerializer.Deserialize<SidecarRoot>(json, Options)
            ?? throw new InvalidOperationException(
                $"Failed to parse tileset sidecar: {sidecarPath}");

        int tileW    = root.TileSize.Length >= 1 ? root.TileSize[0] : 16;
        int tileH    = root.TileSize.Length >= 2 ? root.TileSize[1] : 16;
        int imageCols = System.Math.Max(1, imageWidth  / tileW);

        var tileset = new TilesetData
        {
            Name      = name,
            ImagePath = imagePath,
            TileSize  = new Vector2Int(tileW, tileH),
        };

        foreach (var st in root.Tiles)
        {
            int id = st.Y * imageCols + st.X + 1;
            tileset.Tiles.Add(new TileMetadata
            {
                Id              = new TileId(id),
                Solid           = st.Solid,
                TerrainTag      = st.TerrainTag,
                TerrainPriority = st.TerrainPriority,
                BlobVariant     = st.BlobVariant,
            });
        }

        return tileset;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a <see cref="TilesetData"/> to a sidecar <c>.tileset.json</c> file.
    /// <paramref name="imageCols"/> is required to convert TileIds back to (x, y) pairs.
    /// </summary>
    public static void Save(TilesetData tileset, string path, int imageCols)
    {
        imageCols = System.Math.Max(1, imageCols);

        var root = new SidecarRoot
        {
            TileSize = [tileset.TileSize.X, tileset.TileSize.Y],
        };

        foreach (var meta in tileset.Tiles)
        {
            int idx = meta.Id.Value - 1;
            root.Tiles.Add(new SidecarTile
            {
                X               = idx % imageCols,
                Y               = idx / imageCols,
                Solid           = meta.Solid,
                TerrainTag      = meta.TerrainTag,
                TerrainPriority = meta.TerrainPriority,
                BlobVariant     = meta.BlobVariant,
            });
        }

        File.WriteAllText(path, JsonSerializer.Serialize(root, Options));
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a <see cref="TilesetData"/> by slicing <paramref name="imageWidth"/>×<paramref name="imageHeight"/>
    /// into tiles of <paramref name="tileSize"/>. Each slot gets a <see cref="TileMetadata"/> with
    /// default values (<see cref="TileMetadata.BlobVariant"/> = -1).
    /// </summary>
    public static TilesetData Generate(
        string name, string imagePath,
        int imageWidth, int imageHeight,
        Vector2Int tileSize)
    {
        int cols = System.Math.Max(1, imageWidth  / tileSize.X);
        int rows = System.Math.Max(1, imageHeight / tileSize.Y);

        var tileset = new TilesetData
        {
            Name      = name,
            ImagePath = imagePath,
            TileSize  = tileSize,
        };

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            tileset.Tiles.Add(new TileMetadata
            {
                Id          = new TileId(r * cols + c + 1),
                BlobVariant = -1,
            });

        return tileset;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private sealed class SidecarRoot
    {
        public int[]           TileSize { get; set; } = [16, 16];
        public List<SidecarTile> Tiles  { get; set; } = new();
    }

    private sealed class SidecarTile
    {
        public int    X               { get; set; }
        public int    Y               { get; set; }
        public bool   Solid           { get; set; }
        public string TerrainTag      { get; set; } = "";
        public int    TerrainPriority { get; set; }
        public int    BlobVariant     { get; set; } = -1;
    }
}
