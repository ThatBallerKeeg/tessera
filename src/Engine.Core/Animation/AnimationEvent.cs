namespace Engine.Core.Animation;

/// <summary>
/// A named marker that fires when a specific frame of an <see cref="AnimationClip"/> becomes active.
/// Use for game-logic callbacks such as <c>"footstep_left"</c> or <c>"attack_hit"</c>.
/// </summary>
public sealed class AnimationEvent
{
    /// <summary>
    /// Identifier passed to the event handler, e.g. <c>"footstep_left"</c>.
    /// Should be unique within a clip but is not enforced.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Zero-based index of the frame in <see cref="AnimationClip.Frames"/> on which this
    /// event fires.  Fires exactly once each time the Animator enters this frame index.
    /// Multiple events may share the same <see cref="FrameIndex"/>; all will fire.
    /// </summary>
    public int FrameIndex { get; set; }
}
