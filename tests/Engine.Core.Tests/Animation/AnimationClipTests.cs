using AwesomeAssertions;
using Engine.Core.Animation;
using Engine.Core.Serialization;
using Engine.Core.Sprites;

namespace Engine.Core.Tests.Animation;

public class AnimationClipTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AnimationClip RoundTrip(AnimationClip clip)
    {
        using var ms = new MemoryStream();
        AnimationClipSerializer.Save(clip, ms);
        ms.Position = 0;
        return AnimationClipSerializer.Load(ms);
    }

    private static AnimationClipFrame Frame(int spriteId, int durationMs = 100) =>
        new() { SpriteId = new SpriteId(spriteId), DurationMs = durationMs };

    private static AnimationEvent Evt(string name, int frameIndex) =>
        new() { Name = name, FrameIndex = frameIndex };

    // ── Empty clip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_EmptyClip_PreservesDefaults()
    {
        var loaded = RoundTrip(new AnimationClip { Name = "idle" });

        loaded.Name.Should().Be("idle");
        loaded.Loops.Should().BeTrue("Loops defaults to true");
        loaded.Frames.Should().BeEmpty();
        loaded.Events.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_EmptyClip_EmptyName()
    {
        var loaded = RoundTrip(new AnimationClip());

        loaded.Name.Should().BeEmpty();
    }

    // ── Loops flag ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_LoopsFalse_PreservedAfterRoundTrip()
    {
        var clip   = new AnimationClip { Name = "die", Loops = false };
        clip.Frames.Add(Frame(1, 200));

        var loaded = RoundTrip(clip);

        loaded.Loops.Should().BeFalse("non-looping flag must survive serialization");
    }

    [Fact]
    public void RoundTrip_LoopsTrue_PreservedAfterRoundTrip()
    {
        var clip   = new AnimationClip { Name = "walk", Loops = true };
        clip.Frames.Add(Frame(1));

        var loaded = RoundTrip(clip);

        loaded.Loops.Should().BeTrue();
    }

    // ── Frames ────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SingleFrame_PreservesSpriteIdAndDuration()
    {
        var clip = new AnimationClip { Name = "blink" };
        clip.Frames.Add(Frame(7, 150));

        var loaded = RoundTrip(clip);

        loaded.Frames.Should().HaveCount(1);
        loaded.Frames[0].SpriteId.Should().Be(new SpriteId(7));
        loaded.Frames[0].DurationMs.Should().Be(150);
    }

    [Fact]
    public void RoundTrip_MultipleFrames_AllPreservedInOrder()
    {
        var clip = new AnimationClip { Name = "walk_south" };
        clip.Frames.Add(Frame(1, 100));
        clip.Frames.Add(Frame(2, 120));
        clip.Frames.Add(Frame(3, 100));
        clip.Frames.Add(Frame(4, 80));

        var loaded = RoundTrip(clip);

        loaded.Frames.Should().HaveCount(4);
        loaded.Frames[0].SpriteId.Value.Should().Be(1);
        loaded.Frames[0].DurationMs.Should().Be(100);
        loaded.Frames[1].SpriteId.Value.Should().Be(2);
        loaded.Frames[1].DurationMs.Should().Be(120);
        loaded.Frames[2].SpriteId.Value.Should().Be(3);
        loaded.Frames[3].SpriteId.Value.Should().Be(4);
        loaded.Frames[3].DurationMs.Should().Be(80);
    }

    [Fact]
    public void RoundTrip_FrameWithVariableDurations_AllCorrect()
    {
        // Verify that non-default durations per frame are each preserved independently.
        var clip = new AnimationClip { Name = "attack" };
        clip.Frames.Add(Frame(10, 50));
        clip.Frames.Add(Frame(11, 200));
        clip.Frames.Add(Frame(12, 50));

        var loaded = RoundTrip(clip);

        loaded.Frames.Select(f => f.DurationMs).Should().Equal([50, 200, 50]);
    }

    [Fact]
    public void RoundTrip_SpriteIdEmpty_PreservedInFrames()
    {
        // SpriteId.Empty (0) in a frame must survive; Animator treats it as "blank frame".
        var clip = new AnimationClip { Name = "flash" };
        clip.Frames.Add(new AnimationClipFrame { SpriteId = SpriteId.Empty, DurationMs = 50 });
        clip.Frames.Add(Frame(3, 50));

        var loaded = RoundTrip(clip);

        loaded.Frames[0].SpriteId.Should().Be(SpriteId.Empty);
        loaded.Frames[1].SpriteId.Value.Should().Be(3);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SingleEvent_PreservesNameAndFrameIndex()
    {
        var clip = new AnimationClip { Name = "walk" };
        clip.Frames.Add(Frame(1));
        clip.Frames.Add(Frame(2));
        clip.Events.Add(Evt("footstep_left", 1));

        var loaded = RoundTrip(clip);

        loaded.Events.Should().HaveCount(1);
        loaded.Events[0].Name.Should().Be("footstep_left");
        loaded.Events[0].FrameIndex.Should().Be(1);
    }

    [Fact]
    public void RoundTrip_MultipleEvents_AllPreserved()
    {
        var clip = new AnimationClip { Name = "walk_south" };
        for (int i = 0; i < 6; i++) clip.Frames.Add(Frame(i + 1));
        clip.Events.Add(Evt("footstep_left",  0));
        clip.Events.Add(Evt("footstep_right", 3));
        clip.Events.Add(Evt("dust_puff",      3));   // two events on the same frame

        var loaded = RoundTrip(clip);

        loaded.Events.Should().HaveCount(3);
        loaded.Events[0].Name.Should().Be("footstep_left");
        loaded.Events[0].FrameIndex.Should().Be(0);
        loaded.Events[1].Name.Should().Be("footstep_right");
        loaded.Events[1].FrameIndex.Should().Be(3);
        loaded.Events[2].Name.Should().Be("dust_puff");
        loaded.Events[2].FrameIndex.Should().Be(3,
            "multiple events may share the same FrameIndex");
    }

    [Fact]
    public void RoundTrip_EventAtLastFrame_FrameIndexPreserved()
    {
        var clip = new AnimationClip { Name = "attack" };
        clip.Frames.Add(Frame(1));
        clip.Frames.Add(Frame(2));
        clip.Frames.Add(Frame(3));
        clip.Events.Add(Evt("attack_hit", 2));   // last frame index

        var loaded = RoundTrip(clip);

        loaded.Events[0].FrameIndex.Should().Be(2);
    }

    [Fact]
    public void RoundTrip_EventAtFrame0_FrameIndexPreserved()
    {
        var clip = new AnimationClip { Name = "spawn" };
        clip.Frames.Add(Frame(1));
        clip.Events.Add(Evt("spawn_sound", 0));

        var loaded = RoundTrip(clip);

        loaded.Events[0].FrameIndex.Should().Be(0);
    }

    // ── Combined: full realistic clip ─────────────────────────────────────────

    [Fact]
    public void RoundTrip_FullWalkCycle_AllDataPreserved()
    {
        // Simulates a 4-frame walk_south clip with two footstep events.
        var clip = new AnimationClip
        {
            Name  = "walk_south",
            Loops = true,
            Frames =
            {
                Frame(1, 120),
                Frame(2, 120),
                Frame(3, 120),
                Frame(4, 120),
            },
            Events =
            {
                Evt("footstep_left",  0),
                Evt("footstep_right", 2),
            },
        };

        var loaded = RoundTrip(clip);

        loaded.Name.Should().Be("walk_south");
        loaded.Loops.Should().BeTrue();
        loaded.Frames.Should().HaveCount(4);
        loaded.Frames.Should().AllSatisfy(f => f.DurationMs.Should().Be(120));
        loaded.Frames.Select(f => f.SpriteId.Value).Should().Equal([1, 2, 3, 4]);
        loaded.Events.Should().HaveCount(2);
        loaded.Events[0].Should().BeEquivalentTo(new { Name = "footstep_left",  FrameIndex = 0 });
        loaded.Events[1].Should().BeEquivalentTo(new { Name = "footstep_right", FrameIndex = 2 });
    }

    [Fact]
    public void RoundTrip_NonLoopingDeathClip_PreservesAll()
    {
        var clip = new AnimationClip
        {
            Name  = "die",
            Loops = false,
            Frames =
            {
                Frame(20, 80),
                Frame(21, 80),
                Frame(22, 120),   // hold longer on final frame
            },
            Events =
            {
                Evt("death_sound", 0),
                Evt("ragdoll",     2),
            },
        };

        var loaded = RoundTrip(clip);

        loaded.Loops.Should().BeFalse();
        loaded.Frames[2].DurationMs.Should().Be(120);
        loaded.Events.Select(e => e.Name).Should().Equal(["death_sound", "ragdoll"]);
    }
}
