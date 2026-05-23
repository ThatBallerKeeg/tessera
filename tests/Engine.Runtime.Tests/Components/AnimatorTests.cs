using AwesomeAssertions;
using Engine.Core.Animation;
using Engine.Core.Math;
using Engine.Core.Sprites;
using Engine.Runtime.Components;
using Engine.Runtime.Entities;
using Microsoft.Xna.Framework.Graphics;
using XnaRect  = Microsoft.Xna.Framework.Rectangle;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Engine.Runtime.Tests.Components;

/// <summary>
/// Unit tests for <see cref="Animator"/> playback behaviour and
/// <see cref="SpriteRenderer"/> + <see cref="Animator"/> sibling wiring.
/// </summary>
public class AnimatorTests
{
    // ── Spy ISpriteBatch (reused from SpriteRendererTests pattern) ────────────

    private sealed class SpySpriteBatch : ISpriteBatch
    {
        public record DrawCall(XnaRect Source, XnaRect Destination);
        public List<DrawCall> Calls { get; } = new();

        public void DrawSprite(
            Texture2D? texture, XnaRect source, XnaRect destination, XnaColor tint)
            => Calls.Add(new DrawCall(source, destination));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a clip with <paramref name="frameCount"/> frames of equal duration.
    /// Frame N gets <c>SpriteId(N+1)</c> so SpriteId(1) = frame 0, SpriteId(2) = frame 1, …
    /// </summary>
    private static AnimationClip MakeClip(
        int  frameCount,
        int  durationMs = 100,
        bool loops      = true)
    {
        var clip = new AnimationClip { Loops = loops };
        for (int i = 0; i < frameCount; i++)
            clip.Frames.Add(new AnimationClipFrame
            {
                SpriteId   = new SpriteId(i + 1),
                DurationMs = durationMs,
            });
        return clip;
    }

    /// <summary>
    /// Two-frame spritesheet where frame SpriteId(N) occupies the Nth 16×16 cell.
    /// </summary>
    private static SpritesheetData MakeSpritesheet(int frameCount)
    {
        var sheet = new SpritesheetData { Name = "test", ImagePath = "test.png" };
        for (int i = 0; i < frameCount; i++)
            sheet.Frames.Add(new SpriteFrame
            {
                Id         = new SpriteId(i + 1),
                SourceRect = new Rectangle(i * 16, 0, 16, 16),
                Pivot      = Vector2.Zero,
            });
        return sheet;
    }

    // ── Frame-boundary timing ─────────────────────────────────────────────────

    [Fact]
    public void Play_SetsCurrentSpriteIdToFrame0Immediately()
    {
        var animator = new Animator();
        animator.Play(MakeClip(3));

        animator.CurrentSpriteId.Should().Be(new SpriteId(1),
            "Play() must expose frame-0's sprite before the first OnUpdate");
    }

    [Fact]
    public void OnUpdate_StaysOnFrame0_BeforeDurationElapses()
    {
        var animator = new Animator();
        animator.Play(MakeClip(3, durationMs: 100));

        animator.OnUpdate(0.095f); // 95 ms < 100 ms — still frame 0

        animator.CurrentSpriteId.Should().Be(new SpriteId(1));
    }

    [Fact]
    public void OnUpdate_AdvancesToFrame1_WhenDurationElapses()
    {
        var animator = new Animator();
        animator.Play(MakeClip(3, durationMs: 100));

        animator.OnUpdate(0.105f); // 105 ms ≥ 100 ms — crosses into frame 1

        animator.CurrentSpriteId.Should().Be(new SpriteId(2));
    }

    [Fact]
    public void OnUpdate_AdvancesAcrossAllFrames_AtCorrectBoundaries()
    {
        // 3 frames × 100 ms each → boundaries at 100 ms and 200 ms.
        var animator = new Animator();
        animator.Play(MakeClip(3, durationMs: 100));

        animator.OnUpdate(0.105f); // → frame 1
        animator.CurrentSpriteId.Should().Be(new SpriteId(2), "after 105 ms");

        animator.OnUpdate(0.100f); // → frame 2 (205 ms total)
        animator.CurrentSpriteId.Should().Be(new SpriteId(3), "after 205 ms");
    }

    // ── Looping ───────────────────────────────────────────────────────────────

    [Fact]
    public void LoopingClip_WrapsToFrame0_AfterTotalDurationElapses()
    {
        // 2 frames × 100 ms → total 200 ms.
        var animator = new Animator();
        animator.Play(MakeClip(2, durationMs: 100, loops: true));

        // Advance 250 ms: 250 % 200 = 50 ms → frame 0.
        animator.OnUpdate(0.250f);

        animator.CurrentSpriteId.Should().Be(new SpriteId(1),
            "looping clip must wrap to frame 0 after the total duration elapses");
        animator.IsComplete.Should().BeFalse("looping clips never complete");
    }

    [Fact]
    public void LoopingClip_ContinuesFromWrappedPosition_OnSubsequentTicks()
    {
        // 2 frames × 100 ms.  After wrapping, subsequent ticks should play normally.
        var animator = new Animator();
        animator.Play(MakeClip(2, durationMs: 100, loops: true));

        animator.OnUpdate(0.250f); // wraps to 50 ms, frame 0
        animator.OnUpdate(0.060f); // 50 + 60 = 110 ms → frame 1

        animator.CurrentSpriteId.Should().Be(new SpriteId(2));
    }

    // ── Non-looping / IsComplete ──────────────────────────────────────────────

    [Fact]
    public void NonLoopingClip_SetsIsComplete_WhenTotalDurationElapses()
    {
        var animator = new Animator();
        animator.Play(MakeClip(2, durationMs: 100, loops: false));

        animator.OnUpdate(0.250f); // 250 ms > 200 ms total

        animator.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void NonLoopingClip_HoldsLastFrame_WhenComplete()
    {
        var animator = new Animator();
        animator.Play(MakeClip(2, durationMs: 100, loops: false));

        animator.OnUpdate(0.250f); // completes

        animator.CurrentSpriteId.Should().Be(new SpriteId(2),
            "non-looping clip must hold the final frame after completion");
    }

    [Fact]
    public void NonLoopingClip_DoesNotAdvancePastFinalFrame_OnFurtherUpdates()
    {
        var animator = new Animator();
        animator.Play(MakeClip(2, durationMs: 100, loops: false));

        animator.OnUpdate(0.250f); // complete
        animator.OnUpdate(1.000f); // extra time — must not change anything

        animator.IsComplete.Should().BeTrue();
        animator.CurrentSpriteId.Should().Be(new SpriteId(2));
    }

    // ── Play() reset ──────────────────────────────────────────────────────────

    [Fact]
    public void Play_ResetsElapsedAndFrame_DuringPlayback()
    {
        var clip     = MakeClip(3, durationMs: 100, loops: true);
        var animator = new Animator();
        animator.Play(clip);
        animator.OnUpdate(0.250f); // → frame 2 (SpriteId(3))

        animator.CurrentSpriteId.Should().Be(new SpriteId(3), "pre-condition");

        animator.Play(clip); // reset

        animator.CurrentSpriteId.Should().Be(new SpriteId(1),
            "Play() must restart from frame 0");
    }

    [Fact]
    public void Play_ClearsIsComplete_WhenCalledOnFinishedClip()
    {
        var clip     = MakeClip(2, durationMs: 100, loops: false);
        var animator = new Animator();
        animator.Play(clip);
        animator.OnUpdate(0.300f); // complete

        animator.Play(clip); // restart

        animator.IsComplete.Should().BeFalse();
        animator.CurrentSpriteId.Should().Be(new SpriteId(1));
    }

    // ── AnimationEvent firing ─────────────────────────────────────────────────

    [Fact]
    public void Event_FiresOnce_WhenFrameEntered()
    {
        var clip = new AnimationClip { Loops = true };
        clip.Frames.Add(new AnimationClipFrame { SpriteId = new SpriteId(1), DurationMs = 100 });
        clip.Frames.Add(new AnimationClipFrame { SpriteId = new SpriteId(2), DurationMs = 100 });
        clip.Events.Add(new AnimationEvent { Name = "hit", FrameIndex = 1 });

        var fired    = new List<string>();
        var animator = new Animator();
        animator.EventFired += ev => fired.Add(ev.Name);
        animator.Play(clip);

        // Stay on frame 0 — no event.
        animator.OnUpdate(0.095f); // 95 ms
        fired.Should().BeEmpty("event fires only when entering frame 1, not while on frame 0");

        // Cross into frame 1.
        animator.OnUpdate(0.010f); // 105 ms total
        fired.Should().HaveCount(1);
        fired[0].Should().Be("hit");

        // Remain on frame 1 — no re-fire.
        animator.OnUpdate(0.050f); // 155 ms
        fired.Should().HaveCount(1, "event must not fire again while we stay on frame 1");
    }

    [Fact]
    public void Event_AtFrame0_FiresOnFirstTick()
    {
        var clip = MakeClip(2, durationMs: 100, loops: true);
        clip.Events.Add(new AnimationEvent { Name = "start", FrameIndex = 0 });

        var fired    = new List<string>();
        var animator = new Animator();
        animator.EventFired += ev => fired.Add(ev.Name);
        animator.Play(clip);

        animator.OnUpdate(0.001f); // tiny first tick, stays on frame 0

        fired.Should().HaveCount(1, "frame-0 event fires on the very first tick after Play()");
    }

    [Fact]
    public void Event_FiresAgain_OnLoopWrapToFrame0()
    {
        var clip = MakeClip(2, durationMs: 100, loops: true);
        clip.Events.Add(new AnimationEvent { Name = "tick", FrameIndex = 0 });

        int count    = 0;
        var animator = new Animator();
        animator.EventFired += _ => count++;
        animator.Play(clip);

        // Trigger frame-0 event on first tick.
        animator.OnUpdate(0.001f); // on frame 0, event fires → count = 1
        count.Should().Be(1);

        // Advance to frame 1 — no re-fire.
        animator.OnUpdate(0.105f); // 106 ms, on frame 1
        count.Should().Be(1);

        // Complete the loop and re-enter frame 0.
        animator.OnUpdate(0.100f); // 206 ms → 206 % 200 = 6 ms → frame 0
        count.Should().Be(2, "frame-0 event must fire again when the loop wraps");
    }

    [Fact]
    public void LargeDelta_FiresAllSkippedFrameEvents()
    {
        // 4 frames × 100 ms. One event per frame (e0–e3).
        var clip = new AnimationClip { Loops = true };
        for (int i = 0; i < 4; i++)
            clip.Frames.Add(new AnimationClipFrame
                { SpriteId = new SpriteId(i + 1), DurationMs = 100 });
        clip.Events.Add(new AnimationEvent { Name = "e0", FrameIndex = 0 });
        clip.Events.Add(new AnimationEvent { Name = "e1", FrameIndex = 1 });
        clip.Events.Add(new AnimationEvent { Name = "e2", FrameIndex = 2 });
        clip.Events.Add(new AnimationEvent { Name = "e3", FrameIndex = 3 });

        var fired    = new List<string>();
        var animator = new Animator();
        animator.EventFired += ev => fired.Add(ev.Name);
        animator.Play(clip);

        // Single large tick: 350 ms jumps from prevIdx=-1 directly to frame 3.
        // All four events (frames 0–3) must fire.
        animator.OnUpdate(0.350f);

        fired.Should().HaveCount(4, "every frame that was crossed must fire its event");
        fired.Should().Contain("e0")
             .And.Contain("e1")
             .And.Contain("e2")
             .And.Contain("e3");
    }

    [Fact]
    public void MultipleEventsOnSameFrame_AllFire()
    {
        var clip = MakeClip(2, durationMs: 100);
        clip.Events.Add(new AnimationEvent { Name = "left",  FrameIndex = 1 });
        clip.Events.Add(new AnimationEvent { Name = "right", FrameIndex = 1 });

        var fired    = new List<string>();
        var animator = new Animator();
        animator.EventFired += ev => fired.Add(ev.Name);
        animator.Play(clip);

        animator.OnUpdate(0.105f); // crosses into frame 1

        fired.Should().HaveCount(2, "two events at the same FrameIndex must both fire");
        fired.Should().Contain("left").And.Contain("right");
    }

    // ── SpriteRenderer + Animator sibling wiring ──────────────────────────────

    [Fact]
    public void SpriteRenderer_UsesSiblingAnimatorFrame_NotOwnCurrentFrame()
    {
        // Sheet: SpriteId(1) → x=0, SpriteId(2) → x=16.
        var sheet = MakeSpritesheet(2);

        // Clip: single frame that displays SpriteId(2).
        var clip = new AnimationClip { Loops = true };
        clip.Frames.Add(new AnimationClipFrame { SpriteId = new SpriteId(2), DurationMs = 100 });

        // SpriteRenderer's own field points to SpriteId(1) — should be overridden.
        var go       = new GameObject(Guid.NewGuid(), "hero");
        var renderer = new SpriteRenderer { Spritesheet = sheet, CurrentFrame = new SpriteId(1) };
        var animator = new Animator();
        go.AddComponent(renderer);
        go.AddComponent(animator);

        animator.Play(clip); // CurrentSpriteId = SpriteId(2)

        var spy = new SpySpriteBatch();
        renderer.OnDraw(spy);

        spy.Calls.Should().HaveCount(1);
        spy.Calls[0].Source.Should().Be(new XnaRect(16, 0, 16, 16),
            "SpriteRenderer must draw the Animator's current frame (SpriteId 2, x=16), " +
            "not its own CurrentFrame (SpriteId 1, x=0)");
    }

    [Fact]
    public void SpriteRenderer_FallsBackToOwnCurrentFrame_WhenNoAnimator()
    {
        var sheet    = MakeSpritesheet(2);
        var go       = new GameObject(Guid.NewGuid(), "static");
        var renderer = new SpriteRenderer { Spritesheet = sheet, CurrentFrame = new SpriteId(1) };
        go.AddComponent(renderer); // no Animator added

        var spy = new SpySpriteBatch();
        renderer.OnDraw(spy);

        spy.Calls.Should().HaveCount(1);
        spy.Calls[0].Source.Should().Be(new XnaRect(0, 0, 16, 16),
            "without a sibling Animator, SpriteRenderer uses its own CurrentFrame");
    }

    [Fact]
    public void SpriteRenderer_AnimatorCurrentSpriteIdUpdates_AfterOnUpdate()
    {
        // Two-frame clip: SpriteId(1) then SpriteId(2).  Advance past first frame boundary.
        var sheet    = MakeSpritesheet(2);
        var clip     = MakeClip(2, durationMs: 100, loops: true);
        var go       = new GameObject(Guid.NewGuid(), "hero");
        var renderer = new SpriteRenderer { Spritesheet = sheet, CurrentFrame = new SpriteId(1) };
        var animator = new Animator();
        go.AddComponent(renderer);
        go.AddComponent(animator);

        animator.Play(clip);
        animator.OnUpdate(0.105f); // cross into frame 1 → CurrentSpriteId = SpriteId(2)

        var spy = new SpySpriteBatch();
        renderer.OnDraw(spy);

        spy.Calls[0].Source.Should().Be(new XnaRect(16, 0, 16, 16),
            "after OnUpdate advances the Animator, OnDraw must reflect the new frame");
    }
}
