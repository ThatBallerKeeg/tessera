using Engine.Core.Math;
using FluentAssertions;

namespace Engine.Core.Tests.Math;

public class RectangleIntTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_FourInts_SetsComponents()
    {
        var r = new RectangleInt(1, 2, 3, 4);
        r.X.Should().Be(1);
        r.Y.Should().Be(2);
        r.Width.Should().Be(3);
        r.Height.Should().Be(4);
    }

    [Fact]
    public void Constructor_PositionAndSize_SetsComponents()
    {
        var r = new RectangleInt(new Vector2Int(1, 2), new Vector2Int(3, 4));
        r.X.Should().Be(1); r.Y.Should().Be(2);
        r.Width.Should().Be(3); r.Height.Should().Be(4);
    }

    // ── Computed properties ───────────────────────────────────────────────────

    [Fact]
    public void Edges_AreCorrect()
    {
        var r = new RectangleInt(2, 3, 4, 5);
        r.Left.Should().Be(2);
        r.Right.Should().Be(6);
        r.Top.Should().Be(3);
        r.Bottom.Should().Be(8);
    }

    [Fact]
    public void Center_UsesIntegerDivision()
    {
        // Width/Height 10 → center offset 5
        new RectangleInt(0, 0, 10, 10).Center.Should().Be(new Vector2Int(5, 5));
        // Width/Height 11 → integer division gives 5 (not 5.5)
        new RectangleInt(0, 0, 11, 11).Center.Should().Be(new Vector2Int(5, 5));
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_HasAllZeroComponents()
    {
        var r = RectangleInt.Zero;
        r.X.Should().Be(0); r.Y.Should().Be(0);
        r.Width.Should().Be(0); r.Height.Should().Be(0);
    }

    [Fact]
    public void One_IsUnitRectangleAtOrigin()
    {
        RectangleInt.One.Should().Be(new RectangleInt(0, 0, 1, 1));
    }

    // ── Arithmetic operators ──────────────────────────────────────────────────

    [Fact]
    public void Addition_TranslatesByVector()
    {
        var result = new RectangleInt(1, 2, 3, 4) + new Vector2Int(10, 20);
        result.Should().Be(new RectangleInt(11, 22, 3, 4));
    }

    [Fact]
    public void Subtraction_TranslatesNegatively()
    {
        var result = new RectangleInt(11, 22, 3, 4) - new Vector2Int(10, 20);
        result.Should().Be(new RectangleInt(1, 2, 3, 4));
    }

    [Fact]
    public void MultiplicationByScalar_ScalesAllComponents()
    {
        var result = new RectangleInt(1, 2, 3, 4) * 2;
        result.Should().Be(new RectangleInt(2, 4, 6, 8));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_IsTrue()
    {
        var a = new RectangleInt(1, 2, 3, 4);
        var b = new RectangleInt(1, 2, 3, 4);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_DifferentHeight_IsTrue()
    {
        (new RectangleInt(1, 2, 3, 4) != new RectangleInt(1, 2, 3, 99)).Should().BeTrue();
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_EqualValues_SameHash()
    {
        new RectangleInt(1, 2, 3, 4).GetHashCode()
            .Should().Be(new RectangleInt(1, 2, 3, 4).GetHashCode());
    }

    // ── Lerp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_AtZero_ReturnsA()
    {
        var a = new RectangleInt(0, 0, 10, 10);
        RectangleInt.Lerp(a, new RectangleInt(100, 100, 50, 50), 0f).Should().Be(a);
    }

    [Fact]
    public void Lerp_AtOne_ReturnsB()
    {
        var b = new RectangleInt(100, 100, 50, 50);
        RectangleInt.Lerp(new RectangleInt(0, 0, 10, 10), b, 1f).Should().Be(b);
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var result = RectangleInt.Lerp(
            new RectangleInt(0, 0, 0, 0),
            new RectangleInt(10, 10, 20, 20),
            0.5f);
        result.Should().Be(new RectangleInt(5, 5, 10, 10));
    }

    [Fact]
    public void Lerp_RoundsToNearestInteger()
    {
        // (0 + 10 * 0.35) = 3.5 → rounds to 4
        var result = RectangleInt.Lerp(
            new RectangleInt(0, 0, 0, 0),
            new RectangleInt(10, 10, 10, 10),
            0.35f);
        result.X.Should().Be(4);
    }

    // ── Distance ─────────────────────────────────────────────────────────────

    [Fact]
    public void Distance_CentersApart_ReturnsCorrect()
    {
        // Centers at (5,5) and (5,10) → distance = 5
        var a = new RectangleInt(0, 0, 10, 10);
        var b = new RectangleInt(0, 5, 10, 10);
        RectangleInt.Distance(a, b).Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void Distance_SameRect_ReturnsZero()
    {
        var r = new RectangleInt(0, 0, 10, 10);
        RectangleInt.Distance(r, r).Should().Be(0f);
    }

    // ── Contains ─────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        new RectangleInt(0, 0, 10, 10).Contains(new Vector2Int(5, 5)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOnBoundary_ReturnsTrue()
    {
        new RectangleInt(0, 0, 10, 10).Contains(new Vector2Int(0, 0)).Should().BeTrue();
        new RectangleInt(0, 0, 10, 10).Contains(new Vector2Int(10, 10)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        new RectangleInt(0, 0, 10, 10).Contains(new Vector2Int(11, 5)).Should().BeFalse();
    }

    [Fact]
    public void Contains_RectFullyInside_ReturnsTrue()
    {
        new RectangleInt(0, 0, 10, 10).Contains(new RectangleInt(2, 2, 6, 6)).Should().BeTrue();
    }

    [Fact]
    public void Contains_RectPartiallyOutside_ReturnsFalse()
    {
        new RectangleInt(0, 0, 10, 10).Contains(new RectangleInt(5, 5, 10, 10)).Should().BeFalse();
    }

    // ── Intersects ───────────────────────────────────────────────────────────

    [Fact]
    public void Intersects_OverlappingRects_ReturnsTrue()
    {
        new RectangleInt(0, 0, 10, 10).Intersects(new RectangleInt(5, 5, 10, 10)).Should().BeTrue();
    }

    [Fact]
    public void Intersects_NonOverlappingRects_ReturnsFalse()
    {
        new RectangleInt(0, 0, 5, 5).Intersects(new RectangleInt(10, 10, 5, 5)).Should().BeFalse();
    }

    [Fact]
    public void Intersects_TouchingEdge_ReturnsFalse()
    {
        // Touching at x=10 — open right boundary
        new RectangleInt(0, 0, 10, 10).Intersects(new RectangleInt(10, 0, 10, 10)).Should().BeFalse();
    }
}
