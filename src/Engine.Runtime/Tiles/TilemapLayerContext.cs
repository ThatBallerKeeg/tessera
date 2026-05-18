using Engine.Core.Tiles;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Tiles;

/// <summary>
/// Binds a <see cref="TilemapData"/> layer to its loaded tileset asset for rendering.
/// </summary>
public sealed class TilemapLayerContext
{
    /// <summary>The tilemap layer (tile layout, terrain tags).</summary>
    public required TilemapData Layer { get; init; }

    /// <summary>Tileset metadata (tile size, blob variants, terrain priority).</summary>
    public required TilesetData Tileset { get; init; }

    /// <summary>
    /// Loaded spritesheet texture. May be <see langword="null"/> in test scenarios;
    /// when null, source rectangles are not computed and no pixels are sent to the GPU.
    /// </summary>
    public Texture2D? Texture { get; init; }
}
