using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Entities;

/// <summary>
/// Drawing context passed to <see cref="Component.OnDraw"/>.
/// Implemented by <see cref="SpriteBatchAdapter"/> for the real runtime and by stub
/// implementations in tests.
/// </summary>
public interface ISpriteBatch
{
    /// <summary>
    /// Draws a sub-region of <paramref name="texture"/> into a destination rectangle.
    /// </summary>
    /// <param name="texture">
    /// The GPU texture to sample.  May be <see langword="null"/> in test environments;
    /// <see cref="SpriteBatchAdapter"/> skips the render call when null rather than crashing.
    /// </param>
    /// <param name="source">Source rectangle in texel coordinates.</param>
    /// <param name="destination">Destination rectangle in screen/world pixels.</param>
    /// <param name="tint">Color multiplier applied to every pixel. Use <see cref="Color.White"/> for no tint.</param>
    void DrawSprite(Texture2D? texture, Rectangle source, Rectangle destination, Color tint);
}
