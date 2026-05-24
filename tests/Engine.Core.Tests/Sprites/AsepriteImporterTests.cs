using AwesomeAssertions;
using Engine.Core.Animation;
using Engine.Core.Math;
using Engine.Core.Serialization;
using Engine.Core.Sprites;

namespace Engine.Core.Tests.Sprites;

/// <summary>
/// Tests for <see cref="AsepriteImporter.Import"/> against a fixed Aseprite "Hash" JSON
/// string that covers multiple frames and two frameTags.
/// </summary>
public class AsepriteImporterTests
{
    // ── Test data ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Synthetic Aseprite "Hash" JSON with 6 frames and 2 frameTags.
    /// <list type="bullet">
    ///   <item>frames 0–3 → walk_north (4 frames, varied durations)</item>
    ///   <item>frames 4–5 → idle_south (2 frames, uniform 80 ms)</item>
    ///   <item>Frame 4 starts a new row (y = 16), testing non-zero Y offset.</item>
    /// </list>
    /// </summary>
    private const string SampleJson = """
        {
          "frames": {
            "hero 0.png": { "frame": { "x": 0,  "y": 0,  "w": 16, "h": 16 }, "duration": 100 },
            "hero 1.png": { "frame": { "x": 16, "y": 0,  "w": 16, "h": 16 }, "duration": 120 },
            "hero 2.png": { "frame": { "x": 32, "y": 0,  "w": 16, "h": 16 }, "duration": 100 },
            "hero 3.png": { "frame": { "x": 48, "y": 0,  "w": 16, "h": 16 }, "duration": 150 },
            "hero 4.png": { "frame": { "x": 0,  "y": 16, "w": 16, "h": 16 }, "duration": 80  },
            "hero 5.png": { "frame": { "x": 16, "y": 16, "w": 16, "h": 16 }, "duration": 80  }
          },
          "meta": {
            "image": "hero.png",
            "size": { "w": 64, "h": 32 },
            "frameTags": [
              { "name": "walk_north", "from": 0, "to": 3, "direction": "forward" },
              { "name": "idle_south", "from": 4, "to": 5, "direction": "forward" }
            ]
          }
        }
        """;

    private static (SpritesheetData Sheet, IReadOnlyList<AnimationClip> Clips) Parse()
        => AsepriteImporter.Import(SampleJson);

    // ── SpritesheetData ───────────────────────────────────────────────────────

    [Fact]
    public void Import_FrameCount_MatchesAllFramesInJson()
    {
        var (sheet, _) = Parse();
        sheet.Frames.Should().HaveCount(6, "there are 6 frames in the JSON");
    }

    [Fact]
    public void Import_SheetName_DerivedFromMetaImageFilename()
    {
        var (sheet, _) = Parse();
        sheet.Name.Should().Be("hero", "meta.image is 'hero.png' → name = 'hero'");
    }

    [Fact]
    public void Import_ImagePath_IsMetaImageVerbatim()
    {
        var (sheet, _) = Parse();
        sheet.ImagePath.Should().Be("hero.png", "ImagePath is the raw meta.image value; caller resolves relative path");
    }

    [Fact]
    public void Import_FrameIds_AreOneBasedSequential()
    {
        var (sheet, _) = Parse();
        var ids = sheet.Frames.Select(f => f.Id.Value).ToList();
        ids.Should().Equal(Enumerable.Range(1, 6),
            "SpriteIds start at 1 (0 is SpriteId.Empty) and are sequential");
    }

    [Fact]
    public void Import_FrameNames_MatchJsonKeys()
    {
        var (sheet, _) = Parse();
        sheet.Frames[0].Name.Should().Be("hero 0.png");
        sheet.Frames[5].Name.Should().Be("hero 5.png");
    }

    [Fact]
    public void Import_FirstFrame_CorrectSourceRect()
    {
        var (sheet, _) = Parse();
        sheet.Frames[0].SourceRect.Should().Be(new Rectangle(0, 0, 16, 16),
            "first frame is top-left at (0,0)");
    }

    [Fact]
    public void Import_SecondFrame_HasCorrectXOffset()
    {
        var (sheet, _) = Parse();
        sheet.Frames[1].SourceRect.Should().Be(new Rectangle(16, 0, 16, 16),
            "second frame starts at x=16");
    }

    [Fact]
    public void Import_FifthFrame_HasNonZeroYOffset()
    {
        var (sheet, _) = Parse();
        // Frame 4 (index 4) is the first frame of the second row: y=16.
        sheet.Frames[4].SourceRect.Should().Be(new Rectangle(0, 16, 16, 16),
            "fifth frame starts the second row at y=16");
    }

    [Fact]
    public void Import_AllFramePivots_DefaultToZero()
    {
        var (sheet, _) = Parse();
        sheet.Frames.Should().AllSatisfy(f =>
            f.Pivot.Should().Be(Vector2.Zero, "Aseprite JSON has no pivot data; default is top-left"));
    }

    // ── AnimationClips ────────────────────────────────────────────────────────

    [Fact]
    public void Import_ClipCount_MatchesFrameTagCount()
    {
        var (_, clips) = Parse();
        clips.Should().HaveCount(2, "there are 2 frameTags in the JSON");
    }

    [Fact]
    public void Import_ClipNames_MatchTagNames()
    {
        var (_, clips) = Parse();
        clips[0].Name.Should().Be("walk_north");
        clips[1].Name.Should().Be("idle_south");
    }

    [Fact]
    public void Import_WalkNorth_HasFourFrames()
    {
        var (_, clips) = Parse();
        clips[0].Frames.Should().HaveCount(4, "walk_north covers indices 0–3");
    }

    [Fact]
    public void Import_IdleSouth_HasTwoFrames()
    {
        var (_, clips) = Parse();
        clips[1].Frames.Should().HaveCount(2, "idle_south covers indices 4–5");
    }

    [Fact]
    public void Import_WalkNorth_FirstFrame_HasSpriteId1()
    {
        var (_, clips) = Parse();
        clips[0].Frames[0].SpriteId.Should().Be(new SpriteId(1),
            "first frame of walk_north is frame 0 → SpriteId 1");
    }

    [Fact]
    public void Import_WalkNorth_LastFrame_HasSpriteId4()
    {
        var (_, clips) = Parse();
        clips[0].Frames[3].SpriteId.Should().Be(new SpriteId(4),
            "last frame of walk_north is frame 3 → SpriteId 4");
    }

    [Fact]
    public void Import_IdleSouth_FirstFrame_HasSpriteId5()
    {
        var (_, clips) = Parse();
        clips[1].Frames[0].SpriteId.Should().Be(new SpriteId(5),
            "first frame of idle_south is frame 4 → SpriteId 5");
    }

    [Fact]
    public void Import_FrameDuration_CopiedFromFrameEntry()
    {
        var (_, clips) = Parse();
        // walk_north[0] → "hero 0.png" → duration 100
        clips[0].Frames[0].DurationMs.Should().Be(100);
    }

    [Fact]
    public void Import_DifferentDuration_CopiedCorrectly()
    {
        var (_, clips) = Parse();
        // walk_north[1] → "hero 1.png" → duration 120
        clips[0].Frames[1].DurationMs.Should().Be(120);
        // walk_north[3] → "hero 3.png" → duration 150
        clips[0].Frames[3].DurationMs.Should().Be(150);
    }

    [Fact]
    public void Import_AllClips_LoopByDefault()
    {
        var (_, clips) = Parse();
        clips.Should().AllSatisfy(c =>
            c.Loops.Should().BeTrue("Aseprite direction metadata is not mapped; all clips default to looping"));
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Import_NoFrameTags_ReturnsEmptyClipList()
    {
        const string json = """
            {
              "frames": {
                "tile 0.png": { "frame": { "x": 0, "y": 0, "w": 16, "h": 16 }, "duration": 100 }
              },
              "meta": {
                "image": "tileset.png",
                "size": { "w": 16, "h": 16 }
              }
            }
            """;

        var (_, clips) = AsepriteImporter.Import(json);
        clips.Should().BeEmpty("no frameTags in the JSON means no animation clips");
    }

    [Fact]
    public void Import_EmptyFrameTags_ReturnsEmptyClipList()
    {
        const string json = """
            {
              "frames": {
                "spr 0.png": { "frame": { "x": 0, "y": 0, "w": 8, "h": 8 }, "duration": 100 }
              },
              "meta": {
                "image": "spr.png",
                "frameTags": []
              }
            }
            """;

        var (_, clips) = AsepriteImporter.Import(json);
        clips.Should().BeEmpty();
    }

    [Fact]
    public void Import_MissingFramesProperty_ThrowsInvalidDataException()
    {
        const string json = """{ "meta": { "image": "x.png" } }""";
        var act = () => AsepriteImporter.Import(json);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Import_InvalidJson_ThrowsInvalidDataException()
    {
        var act = () => AsepriteImporter.Import("not valid json {{{{");
        act.Should().Throw<InvalidDataException>();
    }
}
