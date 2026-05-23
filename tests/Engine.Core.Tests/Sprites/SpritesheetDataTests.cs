using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Serialization;
using Engine.Core.Sprites;

namespace Engine.Core.Tests.Sprites;

public class SpritesheetDataTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Round-trips <paramref name="data"/> through SpritesheetSerializer and returns the copy.</summary>
    private static SpritesheetData RoundTrip(SpritesheetData data)
    {
        using var ms = new MemoryStream();
        SpritesheetSerializer.Save(data, ms);
        ms.Position = 0;
        return SpritesheetSerializer.Load(ms);
    }

    // ── SpriteId tests ────────────────────────────────────────────────────────

    [Fact]
    public void SpriteId_Empty_HasValueZero()
        => SpriteId.Empty.Value.Should().Be(0);

    [Fact]
    public void SpriteId_Equality_SameValue_AreEqual()
    {
        var a = new SpriteId(5);
        var b = new SpriteId(5);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void SpriteId_Equality_DifferentValues_AreNotEqual()
    {
        var a = new SpriteId(1);
        var b = new SpriteId(2);
        (a != b).Should().BeTrue();
    }

    // ── Round-trip: empty spritesheet ─────────────────────────────────────────

    [Fact]
    public void RoundTrip_EmptySpritesheet_PreservesNameAndImagePath()
    {
        var original = new SpritesheetData
        {
            Name      = "player",
            ImagePath = "Assets/Sprites/player.png",
        };

        var loaded = RoundTrip(original);

        loaded.Name.Should().Be("player");
        loaded.ImagePath.Should().Be("Assets/Sprites/player.png");
        loaded.Frames.Should().BeEmpty();
    }

    // ── Round-trip: multiple frames with pivots ───────────────────────────────

    [Fact]
    public void RoundTrip_MultipleFrames_PreservesAllFields()
    {
        var original = new SpritesheetData
        {
            Name      = "hero",
            ImagePath = "Assets/hero.png",
            Frames    =
            {
                new SpriteFrame
                {
                    Id         = new SpriteId(1),
                    Name       = "walk_south_0",
                    SourceRect = new Rectangle(0, 0, 16, 16),
                    Pivot      = new Vector2(8, 14),   // foot of the character
                },
                new SpriteFrame
                {
                    Id         = new SpriteId(2),
                    Name       = "walk_south_1",
                    SourceRect = new Rectangle(16, 0, 16, 16),
                    Pivot      = new Vector2(8, 14),
                },
                new SpriteFrame
                {
                    Id         = new SpriteId(3),
                    Name       = "",               // empty name is valid
                    SourceRect = new Rectangle(0, 16, 32, 24),  // non-square frame
                    Pivot      = Vector2.Zero,     // default top-left
                },
            },
        };

        var loaded = RoundTrip(original);

        loaded.Frames.Should().HaveCount(3);

        loaded.Frames[0].Id.Should().Be(new SpriteId(1));
        loaded.Frames[0].Name.Should().Be("walk_south_0");
        loaded.Frames[0].SourceRect.Should().Be(new Rectangle(0, 0, 16, 16));
        loaded.Frames[0].Pivot.Should().Be(new Vector2(8, 14));

        loaded.Frames[1].Id.Should().Be(new SpriteId(2));
        loaded.Frames[1].SourceRect.Should().Be(new Rectangle(16, 0, 16, 16));

        loaded.Frames[2].Id.Should().Be(new SpriteId(3));
        loaded.Frames[2].Name.Should().BeEmpty();
        loaded.Frames[2].SourceRect.Should().Be(new Rectangle(0, 16, 32, 24));
        loaded.Frames[2].Pivot.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void RoundTrip_PivotOffset_PreservesSubPixelPrecision()
    {
        // Pivot can carry fractional pixel values (e.g. true center of an odd-sized frame).
        var original = new SpritesheetData
        {
            Name   = "fx",
            Frames = { new SpriteFrame { Id = new SpriteId(1), SourceRect = new Rectangle(0, 0, 15, 15), Pivot = new Vector2(7.5f, 7.5f) } },
        };

        var loaded = RoundTrip(original);

        loaded.Frames[0].Pivot.X.Should().BeApproximately(7.5f, 1e-5f);
        loaded.Frames[0].Pivot.Y.Should().BeApproximately(7.5f, 1e-5f);
    }

    [Fact]
    public void RoundTrip_SpriteId_EmptyPreserved()
    {
        // SpriteId.Empty (0) must survive the round-trip.
        var original = new SpritesheetData
        {
            Name   = "test",
            Frames = { new SpriteFrame { Id = SpriteId.Empty, SourceRect = new Rectangle(0, 0, 16, 16) } },
        };

        var loaded = RoundTrip(original);

        loaded.Frames[0].Id.Should().Be(SpriteId.Empty);
    }

    // ── GenerateGrid ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateGrid_64x64Image_16x16Frames_Produces16Frames()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "tiles",
            "Assets/tiles.png",
            new Vector2Int(64, 64),
            new Vector2Int(16, 16));

        sheet.Frames.Should().HaveCount(16, "64/16 = 4 cols × 4 rows = 16 cells");
    }

    [Fact]
    public void GenerateGrid_FrameIds_AreOneBasedSequential()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(16, 16));

        var ids = sheet.Frames.Select(f => f.Id.Value).ToList();
        ids.Should().Equal(Enumerable.Range(1, 16),
            "IDs start at 1 (0 is SpriteId.Empty) and are sequential");
    }

    [Fact]
    public void GenerateGrid_FirstFrame_CorrectSourceRect()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(16, 16));

        sheet.Frames[0].SourceRect.Should().Be(new Rectangle(0, 0, 16, 16),
            "first frame is top-left cell");
    }

    [Fact]
    public void GenerateGrid_RowMajorOrder_SecondFrameIsColOne()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(16, 16));

        // 4-column grid: frame[1] = col 1, row 0 → x=16, y=0
        sheet.Frames[1].SourceRect.Should().Be(new Rectangle(16, 0, 16, 16),
            "second frame is col 1 in the first row");
    }

    [Fact]
    public void GenerateGrid_FifthFrame_StartsSecondRow()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(16, 16));

        // 4 cols × 4 rows; frame[4] = index 4 → col 0, row 1 → x=0, y=16
        sheet.Frames[4].SourceRect.Should().Be(new Rectangle(0, 16, 16, 16),
            "fifth frame starts the second row");
    }

    [Fact]
    public void GenerateGrid_LastFrame_IsBottomRight()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(16, 16));

        // frame[15] = col 3, row 3 → x=48, y=48
        sheet.Frames[15].SourceRect.Should().Be(new Rectangle(48, 48, 16, 16),
            "last frame is the bottom-right cell");
    }

    [Fact]
    public void GenerateGrid_AllPivotsDefaultToZero()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(32, 16), new Vector2Int(16, 16));

        sheet.Frames.Should().AllSatisfy(f =>
            f.Pivot.Should().Be(Vector2.Zero, "GenerateGrid does not set pivot; default is top-left"));
    }

    [Fact]
    public void GenerateGrid_MetadataPreserved()
    {
        var sheet = SpritesheetData.GenerateGrid(
            "player", "Assets/player.png",
            new Vector2Int(32, 16), new Vector2Int(16, 16));

        sheet.Name.Should().Be("player");
        sheet.ImagePath.Should().Be("Assets/player.png");
    }

    [Fact]
    public void GenerateGrid_NonSquareImage_CorrectFrameCount()
    {
        // 96×32 image, 16×16 frames → 6 cols × 2 rows = 12 frames.
        var sheet = SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(96, 32), new Vector2Int(16, 16));

        sheet.Frames.Should().HaveCount(12);
        sheet.Frames[6].SourceRect.Should().Be(new Rectangle(0, 16, 16, 16),
            "seventh frame is first frame of second row");
    }

    [Fact]
    public void GenerateGrid_UnevenDivision_ThrowsArgumentException()
    {
        // 64×64 image cannot be divided into 12×12 cells.
        var act = () => SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(12, 12));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateGrid_ZeroFrameSize_ThrowsArgumentException()
    {
        var act = () => SpritesheetData.GenerateGrid(
            "s", "", new Vector2Int(64, 64), new Vector2Int(0, 16));

        act.Should().Throw<ArgumentException>();
    }

    // ── GenerateGrid round-trips through SpritesheetSerializer ───────────────

    [Fact]
    public void GenerateGrid_RoundTrip_PreservesFrameCount()
    {
        var original = SpritesheetData.GenerateGrid(
            "tileset", "tiles.png",
            new Vector2Int(64, 64), new Vector2Int(16, 16));

        var loaded = RoundTrip(original);

        loaded.Frames.Should().HaveCount(16);
    }

    [Fact]
    public void GenerateGrid_RoundTrip_PreservesSourceRects()
    {
        var original = SpritesheetData.GenerateGrid(
            "tileset", "tiles.png",
            new Vector2Int(64, 64), new Vector2Int(16, 16));

        var loaded = RoundTrip(original);

        for (int i = 0; i < 16; i++)
            loaded.Frames[i].SourceRect.Should().Be(original.Frames[i].SourceRect,
                $"frame {i} source rect must survive round-trip");
    }
}
