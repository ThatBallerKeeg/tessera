namespace Engine.Core.Animation;

/// <summary>
/// An ordered sequence of sprite frames that plays at runtime via the <c>Animator</c> component.
/// </summary>
/// <remarks>
/// Clip names conventionally match Aseprite tag names (e.g. <c>"walk_north"</c>, <c>"idle_south"</c>)
/// so that the Aseprite importer can correlate tags to clips by name.
/// <para>
/// Non-looping clips (<see cref="Loops"/> = false) are the mechanism for one-shot animated
/// sprites (hit flashes, footstep dust) per the Phase 2 particle decision in PLAN.md.
/// </para>
/// </remarks>
public sealed class AnimationClip
{
    /// <summary>
    /// Display name, conventionally matching the Aseprite tag name (e.g. <c>"walk_south"</c>).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>Ordered frames that make up the animation.</summary>
    public List<AnimationClipFrame> Frames { get; set; } = new();

    /// <summary>
    /// When true (default), the clip wraps back to frame 0 after the final frame's duration
    /// elapses.  When false, the Animator holds the final frame and marks itself complete.
    /// </summary>
    public bool Loops { get; set; } = true;

    /// <summary>
    /// Named markers that fire when their target frame becomes active.
    /// Order within the list does not affect firing order; events are matched by
    /// <see cref="AnimationEvent.FrameIndex"/>.
    /// </summary>
    public List<AnimationEvent> Events { get; set; } = new();
}
