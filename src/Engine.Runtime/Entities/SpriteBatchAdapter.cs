using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Entities;

/// <summary>
/// Wraps a MonoGame <see cref="SpriteBatch"/> and implements <see cref="ISpriteBatch"/> so
/// engine components can draw sprites without a hard reference to MonoGame types.
/// <para>
/// Phase 2.4 adds <c>DrawSprite</c> overloads once <c>SpriteRenderer</c> is implemented.
/// </para>
/// </summary>
public sealed class SpriteBatchAdapter : ISpriteBatch
{
    /// <summary>The underlying MonoGame batch. Use for Phase 2.4 extension methods.</summary>
    public SpriteBatch Inner { get; }

    /// <summary>Creates an adapter wrapping <paramref name="spriteBatch"/>.</summary>
    public SpriteBatchAdapter(SpriteBatch spriteBatch)
    {
        Inner = spriteBatch;
    }
}
