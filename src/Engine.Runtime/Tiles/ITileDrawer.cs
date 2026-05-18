using Microsoft.Xna.Framework;

namespace Engine.Runtime.Tiles;

/// <summary>
/// Abstraction over a single tile draw call. Implemented by <see cref="SpriteBatchTileDrawer"/>
/// in production and by test doubles in unit tests.
/// </summary>
public interface ITileDrawer
{
    /// <summary>Draws one tile quad at the given pixel coordinates.</summary>
    void Draw(Rectangle destinationRect, Rectangle sourceRect);
}
