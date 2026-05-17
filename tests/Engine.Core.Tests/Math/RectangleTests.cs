using Engine.Core.Math;
using AwesomeAssertions;

namespace Engine.Core.Tests.Math;

public class RectangleTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_FourFloats_SetsComponents()
    {
        var r = new Rectangle(1f, 2f, 3f, 4f);
        r.X.Should().Be(1f);
        r.Y.Should().Be(2f);
        r.Width.Should().Be(3f);
        r.Height.Should().Be(4f);
    }

    [Fact]
    public void Constructor_PositionAndSize_SetsComponents()
    {
        var r = new Rectangle(new Vector2(1f, 2f), new Vector2(3f, 4f));
        r.X.Should().Be(1f);
        r.Y.Should().Be(2f);
        r.Width.Should().Be(3f);
        r.Height.Should().Be(4f);
    }

    // ── Computed properties ───────────────────────────────────────────────────

    [Fact]
    public void Edges_AreCorrect()
    {
        var r = new Rectangle(2f, 3f, 4f, 5f);
        r.Left.Should().Be(2f);
        r.Right.Should().Be(6f);
        r.Top.Should().Be(3f);
        r.Bottom.Should().Be(8f);
    }

    [Fact]
    public void Center_IsCorrect()
    {
        var r = new Rectangle(0f, 0f, 10f, 6f);
        r.Center.X.Should().BeApproximately(5f, 1e-5f);
        r.Center.Y.Should().BeApproximately(3f, 1e-5f);
    }

    [Fact]
    public void Position_ReturnsTopLeft()
    {
        var r = new Rectangle(1f, 2f, 3f, 4f);
        r.Position.Should().Be(new Vector2(1f, 2f));
    }

    [Fact]
    public void Size_ReturnsWidthAndHeight()
    {
        var r = new Rectangle(1f, 2f, 3f, 4f);
        r.Size.Should().Be(new Vector2(3f, 4f));
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_HasAllZeroComponents()
    {
        var r = Rectangle.Zero;
        r.X.Should().Be(0f); r.Y.Should().Be(0f);
        r.Width.Should().Be(0f); r.Height.Should().Be(0f);
    }

    [Fact]
    public void One_IsUnitRectangleAtOrigin()
    {
        Rectangle.One.Should().Be(new Rectangle(0f, 0f, 1f, 1f));
    }

    // ── Arithmetic operators ──────────────────────────────────────────────────

    [Fact]
    public void Addition_TranslatesByVector()
    {
        var result = new Rectangle(1f, 2f, 3f, 4f) + new Vector2(10f, 20f);
        result.Should().Be(new Rectangle(11f, 22f, 3f, 4f));
    }

    [Fact]
    public void Subtraction_TranslatesNegatively()
    {
        var result = new Rectangle(11f, 22f, 3f, 4f) - new Vector2(10f, 20f);
        result.Should().Be(new Rectangle(1f, 2f, 3f, 4f));
    }

    [Fact]
    public void MultiplicationByScalar_ScalesAllComponents()
    {
        var result = new Rectangle(1f, 2f, 3f, 4f) * 2f;
        result.Should().Be(new Rectangle(2f, 4f, 6f, 8f));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_IsTrue()
    {
        var a = new Rectangle(1f, 2f, 3f, 4f);
        var b = new Rectangle(1f, 2f, 3f, 4f);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_DifferentWidth_IsTrue()
    {
        (new Rectangle(1f, 2f, 3f, 4f) != new Rectangle(1f, 2f, 99f, 4f)).Should().BeTrue();
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_EqualValues_SameHash()
    {
        new Rectangle(1f, 2f, 3f, 4f).GetHashCode()
            .Should().Be(new Rectangle(1f, 2f, 3f, 4f).GetHashCode());
    }

    // ── Lerp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_AtZero_ReturnsA()
    {
        var a = new Rectangle(0f, 0f, 10f, 10f);
        var b = new Rectangle(100f, 100f, 50f, 50f);
        Rectangle.Lerp(a, b, 0f).Should().Be(a);
    }

    [Fact]
    public void Lerp_AtOne_ReturnsB()
    {
        var a = new Rectangle(0f, 0f, 10f, 10f);
        var b = new Rectangle(100f, 100f, 50f, 50f);
        Rectangle.Lerp(a, b, 1f).Should().Be(b);
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var result = Rectangle.Lerp(
            new Rectangle(0f, 0f, 0f, 0f),
            new Rectangle(10f, 10f, 20f, 20f),
            0.5f);
        result.X.Should().BeApproximately(5f, 1e-5f);
        result.Y.Should().BeApproximately(5f, 1e-5f);
        result.Width.Should().BeApproximately(10f, 1e-5f);
        result.Height.Should().BeApproximately(10f, 1e-5f);
    }

    // ── Distance ─────────────────────────────────────────────────────────────

    [Fact]
    public void Distance_CentersApart_ReturnsCorrect()
    {
        // Centers at (5,5) and (5,10) → distance = 5
        var a = new Rectangle(0f, 0f, 10f, 10f);
        var b = new Rectangle(0f, 5f, 10f, 10f);
        Rectangle.Distance(a, b).Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void Distance_SameCenter_ReturnsZero()
    {
        var a = new Rectangle(0f, 0f, 10f, 10f);
        Rectangle.Distance(a, a).Should().Be(0f);
    }

    // ── Contains ─────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Vector2(5f, 5f)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOnBoundary_ReturnsTrue()
    {
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Vector2(0f, 0f)).Should().BeTrue();
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Vector2(10f, 10f)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Vector2(11f, 5f)).Should().BeFalse();
    }

    [Fact]
    public void Contains_RectangleFullyInside_ReturnsTrue()
    {
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Rectangle(2f, 2f, 6f, 6f)).Should().BeTrue();
    }

    [Fact]
    public void Contains_RectanglePartiallyOutside_ReturnsFalse()
    {
        new Rectangle(0f, 0f, 10f, 10f).Contains(new Rectangle(5f, 5f, 10f, 10f)).Should().BeFalse();
    }

    // ── Intersects ───────────────────────────────────────────────────────────

    [Fact]
    public void Intersects_OverlappingRectangles_ReturnsTrue()
    {
        new Rectangle(0f, 0f, 10f, 10f).Intersects(new Rectangle(5f, 5f, 10f, 10f)).Should().BeTrue();
    }

    [Fact]
    public void Intersects_NonOverlappingRectangles_ReturnsFalse()
    {
        new Rectangle(0f, 0f, 5f, 5f).Intersects(new Rectangle(10f, 10f, 5f, 5f)).Should().BeFalse();
    }

    [Fact]
    public void Intersects_TouchingEdge_ReturnsFalse()
    {
        // Touching at x=10 — open boundary on the right
        new Rectangle(0f, 0f, 10f, 10f).Intersects(new Rectangle(10f, 0f, 10f, 10f)).Should().BeFalse();
    }
}
