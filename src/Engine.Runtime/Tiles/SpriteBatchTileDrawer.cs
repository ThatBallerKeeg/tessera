using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.Tiles;

/// <summary>
/// <see cref="ITileDrawer"/> that forwards draw calls to a MonoGame <see cref="SpriteBatch"/>.
/// The SpriteBatch must already be open (between Begin and End) when <see cref="Draw"/> is called.
/// </summary>
public sealed class SpriteBatchTileDrawer : ITileDrawer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D? _texture;

    /// <param name="spriteBatch">An active SpriteBatch (already in Begin/End scope).</param>
    /// <param name="texture">The tileset spritesheet texture. If null, draw calls are no-ops.</param>
    public SpriteBatchTileDrawer(SpriteBatch spriteBatch, Texture2D? texture)
    {
        _spriteBatch = spriteBatch;
        _texture     = texture;
    }

    /// <inheritdoc/>
    public void Draw(Rectangle destinationRect, Rectangle sourceRect)
    {
        if (_texture is null) return;
        _spriteBatch.Draw(_texture, destinationRect, sourceRect, Color.White);
    }
}
