using Engine.Core.Sprites;

namespace Engine.Core.Animation;

/// <summary>
/// One frame in an <see cref="AnimationClip"/>: the sprite to display and how long to hold it.
/// </summary>
public sealed class AnimationClipFrame
{
    /// <summary>The sprite to display while this frame is active.</summary>
    public SpriteId SpriteId { get; set; }

    /// <summary>How long this frame is shown, in milliseconds. Must be positive.</summary>
    public int DurationMs { get; set; } = 100;
}
