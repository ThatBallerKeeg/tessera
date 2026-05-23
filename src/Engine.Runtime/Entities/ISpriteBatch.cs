namespace Engine.Runtime.Entities;

/// <summary>
/// Drawing context passed to <see cref="Component.OnDraw"/>.
/// Implemented by <see cref="SpriteBatchAdapter"/> for the real runtime and by stub
/// implementations in tests.
/// <para>
/// Phase 2.4 adds <c>DrawSprite</c> methods once <c>SpriteRenderer</c> is fleshed out.
/// </para>
/// </summary>
public interface ISpriteBatch
{
    // Phase 2.4: void DrawSprite(Texture2D texture, Vector2 position, Rectangle sourceRect, Vector2 pivot, Color tint);
}
