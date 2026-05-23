using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Sprites;
using Engine.Runtime.Components;
using Engine.Runtime.Entities;
using Microsoft.Xna.Framework.Graphics;
using XnaRect  = Microsoft.Xna.Framework.Rectangle;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Engine.Runtime.Tests.Components;

/// <summary>
/// Unit tests for <see cref="SpriteRenderer.OnDraw"/>:
/// verifies draw-call count, source rect accuracy, destination rect with pivot offset,
/// and graceful skips for empty SpriteId / null Spritesheet.
/// </summary>
public class SpriteRendererTests
{
    // ── Spy ISpriteBatch ──────────────────────────────────────────────────────

    /// <summary>
    /// Records every <c>DrawSprite</c> call so tests can assert on count and parameters
    /// without needing a real GraphicsDevice.
    /// </summary>
    private sealed class SpySpriteBatch : ISpriteBatch
    {
        public record DrawCall(XnaRect Source, XnaRect Destination);

        public List<DrawCall> Calls { get; } = new();

        public void DrawSprite(
            Texture2D? texture,
            XnaRect source,
            XnaRect destination,
            XnaColor tint)
            => Calls.Add(new DrawCall(source, destination));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A two-frame spritesheet:
    ///  Frame SpriteId(1) — top-left 16×16, pivot (0, 0)
    ///  Frame SpriteId(2) — next 16×16 cell to the right, pivot (8, 8) (center)
    /// </summary>
    private static SpritesheetData MakeSpritesheet()
    {
        var sheet = new SpritesheetData { Name = "test", ImagePath = "test.png" };
        sheet.Frames.Add(new SpriteFrame
        {
            Id         = new SpriteId(1),
            SourceRect = new Rectangle(0, 0, 16, 16),
            Pivot      = Vector2.Zero,
        });
        sheet.Frames.Add(new SpriteFrame
        {
            Id         = new SpriteId(2),
            SourceRect = new Rectangle(16, 0, 16, 16),
            Pivot      = new Vector2(8, 8),  // center pivot
        });
        return sheet;
    }

    /// <summary>Creates a live GameObject and attaches <paramref name="renderer"/> to it.</summary>
    private static GameObject Attach(SpriteRenderer renderer, float x = 0f, float y = 0f)
    {
        var go = new GameObject(Guid.NewGuid(), "test") { Position = new Vector2(x, y) };
        go.AddComponent(renderer);
        return go;
    }

    // ── Draw call count ───────────────────────────────────────────────────────

    [Fact]
    public void OnDraw_NonEmptySpriteId_IssuesOneDrawCall()
    {
        var spy      = new SpySpriteBatch();
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = new SpriteId(1) };
        Attach(renderer);

        renderer.OnDraw(spy);

        spy.Calls.Should().HaveCount(1, "a valid SpriteId must produce exactly one draw call");
    }

    [Fact]
    public void OnDraw_EmptySpriteId_ZeroDrawCalls()
    {
        var spy      = new SpySpriteBatch();
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = SpriteId.Empty };
        Attach(renderer);

        renderer.OnDraw(spy);

        spy.Calls.Should().BeEmpty("SpriteId.Empty must suppress the draw call without crashing");
    }

    [Fact]
    public void OnDraw_NullSpritesheet_ZeroDrawCalls()
    {
        var spy      = new SpySpriteBatch();
        var renderer = new SpriteRenderer { Spritesheet = null, CurrentFrame = new SpriteId(1) };
        Attach(renderer);

        renderer.OnDraw(spy);

        spy.Calls.Should().BeEmpty("null Spritesheet must suppress the draw call without crashing");
    }

    // ── Source rectangle ──────────────────────────────────────────────────────

    [Fact]
    public void OnDraw_Frame1_CorrectSourceRect()
    {
        var spy      = new SpySpriteBatch();
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = new SpriteId(1) };
        Attach(renderer);

        renderer.OnDraw(spy);

        spy.Calls[0].Source.Should().Be(new XnaRect(0, 0, 16, 16),
            "frame 1 occupies the first 16×16 cell at (0,0)");
    }

    [Fact]
    public void OnDraw_Frame2_CorrectSourceRect()
    {
        var spy      = new SpySpriteBatch();
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = new SpriteId(2) };
        Attach(renderer);

        renderer.OnDraw(spy);

        spy.Calls[0].Source.Should().Be(new XnaRect(16, 0, 16, 16),
            "frame 2 occupies the second 16×16 cell at (16,0)");
    }

    // ── Destination rectangle (position + pivot) ──────────────────────────────

    [Fact]
    public void OnDraw_ZeroPivot_DestinationEqualsPosition()
    {
        var spy      = new SpySpriteBatch();
        // Frame 1 has pivot (0,0) — destination top-left should equal Position.
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = new SpriteId(1) };
        Attach(renderer, x: 100f, y: 200f);

        renderer.OnDraw(spy);

        spy.Calls[0].Destination.Should().Be(new XnaRect(100, 200, 16, 16),
            "zero pivot means the sprite top-left is placed directly at the GameObject's position");
    }

    [Fact]
    public void OnDraw_CenterPivot_DestinationOffsetByPivot()
    {
        var spy      = new SpySpriteBatch();
        // Frame 2 has pivot (8,8) — destination shifts by -8 on each axis.
        var renderer = new SpriteRenderer { Spritesheet = MakeSpritesheet(), CurrentFrame = new SpriteId(2) };
        Attach(renderer, x: 100f, y: 200f);

        renderer.OnDraw(spy);

        // Expected: (100-8, 200-8, 16, 16) = (92, 192, 16, 16)
        spy.Calls[0].Destination.Should().Be(new XnaRect(92, 192, 16, 16),
            "center pivot (8,8) shifts the destination rect so the sprite is centered on Position");
    }
}
