using Engine.Runtime.Entities;

namespace Engine.Runtime.Components;

/// <summary>
/// Drives an <c>AnimationClip</c>, advancing frames per <see cref="Component.OnUpdate"/>
/// and exposing the current <c>SpriteId</c> for a sibling <see cref="SpriteRenderer"/> to read.
/// <para>
/// Shell for Task 2.1 — full implementation in Task 2.4 (clip playback, event markers,
/// <c>IsComplete</c>, <c>Play(clip)</c>).
/// </para>
/// </summary>
public sealed class Animator : Component
{
    // Phase 2.4: AnimationClip? CurrentClip, SpriteId CurrentSpriteId, bool IsComplete, Play(AnimationClip).
}
