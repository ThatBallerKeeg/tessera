using Engine.Core.Sprites;
using Engine.Runtime.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace Engine.Runtime.Components;

/// <summary>
/// Renders a single sprite frame from a spritesheet at the owner's world position.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="CurrentFrame"/> to the <see cref="SpriteId"/> you want displayed each frame.
/// If a sibling <see cref="Animator"/> is attached, it writes to <see cref="CurrentFrame"/>
/// each update so the renderer always reflects the animation state without additional wiring.
/// </para>
/// <para>
/// <see cref="Texture"/> must be populated before the first draw call — typically by the scene
/// loader using the shared <see cref="TextureCache"/>.  When null (e.g. in test environments)
/// the draw call is still issued and counted at the <see cref="ISpriteBatch"/> level, but
/// <see cref="SpriteBatchAdapter"/> suppresses the actual GPU upload.
/// </para>
/// </remarks>
public sealed class SpriteRenderer : Component
{
    // ── Public data ───────────────────────────────────────────────────────────

    /// <summary>Spritesheet asset that provides frame geometry for this renderer.</summary>
    public SpritesheetData? Spritesheet { get; set; }

    /// <summary>
    /// The frame to display this render frame.
    /// <see cref="SpriteId.Empty"/> suppresses the draw call (clean skip, no crash).
    /// </summary>
    public SpriteId CurrentFrame { get; set; }

    /// <summary>
    /// GPU texture for <see cref="SpritesheetData.ImagePath"/>.
    /// Set externally via <see cref="TextureCache"/> at scene-load time.
    /// Null is safe — <see cref="SpriteBatchAdapter.DrawSprite"/> silently no-ops.
    /// </summary>
    public Texture2D? Texture { get; set; }

    // ── Component lifecycle ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void OnDraw(ISpriteBatch spriteBatch)
    {
        if (Spritesheet is null) return;

        // Prefer a sibling Animator's current frame; fall back to our own field
        // so SpriteRenderer works for static (un-animated) sprites as well.
        var animator = Owner?.GetComponent<Animator>();
        var frameId  = animator is not null ? animator.CurrentSpriteId : CurrentFrame;
        if (frameId == SpriteId.Empty) return;

        // Linear search over the frames list — avoid LINQ allocation on a hot path.
        SpriteFrame? frame = null;
        foreach (var f in Spritesheet.Frames)
        {
            if (f.Id == frameId) { frame = f; break; }
        }
        if (frame is null) return;

        // Source rectangle: the sub-region of the spritesheet PNG.
        var src = new XnaRectangle(
            (int)frame.SourceRect.X,
            (int)frame.SourceRect.Y,
            (int)frame.SourceRect.Width,
            (int)frame.SourceRect.Height);

        // Destination rectangle: world position minus pivot so the sprite is
        // anchored at the GameObject's world point, not at its top-left corner.
        var pos = Owner!.Position;
        var dest = new XnaRectangle(
            (int)(pos.X - frame.Pivot.X),
            (int)(pos.Y - frame.Pivot.Y),
            src.Width,
            src.Height);

        spriteBatch.DrawSprite(Texture, src, dest, Color.White);
    }
}
