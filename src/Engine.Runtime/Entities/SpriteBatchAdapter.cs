using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Entities;

/// <summary>
/// Wraps a MonoGame <see cref="SpriteBatch"/> and implements <see cref="ISpriteBatch"/> so
/// engine components can draw sprites without a hard reference to MonoGame types.
/// </summary>
public sealed class SpriteBatchAdapter : ISpriteBatch
{
    /// <summary>The underlying MonoGame batch.</summary>
    public SpriteBatch Inner { get; }

    /// <summary>Creates an adapter wrapping <paramref name="spriteBatch"/>.</summary>
    public SpriteBatchAdapter(SpriteBatch spriteBatch)
    {
        Inner = spriteBatch;
    }

    /// <inheritdoc/>
    /// <remarks>Silently skips the draw call when <paramref name="texture"/> is null.</remarks>
    public void DrawSprite(Texture2D? texture, Rectangle source, Rectangle destination, Color tint)
    {
        if (texture is null) return;
        Inner.Draw(texture, destination, source, tint);
    }
}
