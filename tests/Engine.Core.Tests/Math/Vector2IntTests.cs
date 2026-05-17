using Engine.Core.Math;
using AwesomeAssertions;

namespace Engine.Core.Tests.Math;

public class Vector2IntTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsComponents()
    {
        var v = new Vector2Int(3, 4);
        v.X.Should().Be(3);
        v.Y.Should().Be(4);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_HasZeroComponents()
    {
        Vector2Int.Zero.X.Should().Be(0);
        Vector2Int.Zero.Y.Should().Be(0);
    }

    [Fact]
    public void One_HasOneComponents()
    {
        Vector2Int.One.X.Should().Be(1);
        Vector2Int.One.Y.Should().Be(1);
    }

    // ── Arithmetic operators ──────────────────────────────────────────────────

    [Fact]
    public void Addition_ReturnsComponentwiseSum()
    {
        var result = new Vector2Int(1, 2) + new Vector2Int(3, 4);
        result.Should().Be(new Vector2Int(4, 6));
    }

    [Fact]
    public void Subtraction_ReturnsComponentwiseDifference()
    {
        var result = new Vector2Int(5, 6) - new Vector2Int(2, 1);
        result.Should().Be(new Vector2Int(3, 5));
    }

    [Fact]
    public void UnaryNegation_NegatesComponents()
    {
        var result = -new Vector2Int(1, -2);
        result.Should().Be(new Vector2Int(-1, 2));
    }

    [Fact]
    public void MultiplicationByScalar_ScalesComponents()
    {
        var result = new Vector2Int(2, 3) * 4;
        result.Should().Be(new Vector2Int(8, 12));
    }

    [Fact]
    public void MultiplicationByScalar_IsCommutative()
    {
        var v = new Vector2Int(2, 3);
        (4 * v).Should().Be(v * 4);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_IsTrue()
    {
        var a = new Vector2Int(1, 2);
        var b = new Vector2Int(1, 2);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_DifferentValues_IsTrue()
    {
        (new Vector2Int(1, 2) != new Vector2Int(1, 3)).Should().BeTrue();
    }

    [Fact]
    public void Equals_BoxedValue_WorksViaObjectOverride()
    {
        var a = new Vector2Int(1, 2);
        object b = new Vector2Int(1, 2);
        a.Equals(b).Should().BeTrue();
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_EqualValues_SameHash()
    {
        new Vector2Int(1, 2).GetHashCode().Should().Be(new Vector2Int(1, 2).GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_DifferentHash()
    {
        new Vector2Int(1, 2).GetHashCode().Should().NotBe(new Vector2Int(2, 1).GetHashCode());
    }

    // ── Dot ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Dot_PerpendicularVectors_ReturnsZero()
    {
        Vector2Int.Dot(new Vector2Int(1, 0), new Vector2Int(0, 1)).Should().Be(0);
    }

    [Fact]
    public void Dot_ArbitraryVectors_ReturnsExpected()
    {
        Vector2Int.Dot(new Vector2Int(2, 3), new Vector2Int(4, 5)).Should().Be(23);
    }

    // ── Distance ─────────────────────────────────────────────────────────────

    [Fact]
    public void Distance_KnownTriangle_Returns5()
    {
        Vector2Int.Distance(Vector2Int.Zero, new Vector2Int(3, 4)).Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void Distance_SamePoint_ReturnsZero()
    {
        Vector2Int.Distance(new Vector2Int(2, 3), new Vector2Int(2, 3)).Should().Be(0f);
    }

    [Fact]
    public void Distance_IsSymmetric()
    {
        var a = new Vector2Int(1, 2);
        var b = new Vector2Int(4, 6);
        Vector2Int.Distance(a, b).Should().BeApproximately(Vector2Int.Distance(b, a), 1e-6f);
    }

    // ── Lerp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_AtZero_ReturnsA()
    {
        Vector2Int.Lerp(new Vector2Int(0, 0), new Vector2Int(10, 10), 0f)
            .Should().Be(new Vector2Int(0, 0));
    }

    [Fact]
    public void Lerp_AtOne_ReturnsB()
    {
        Vector2Int.Lerp(new Vector2Int(0, 0), new Vector2Int(10, 10), 1f)
            .Should().Be(new Vector2Int(10, 10));
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        Vector2Int.Lerp(new Vector2Int(0, 0), new Vector2Int(10, 10), 0.5f)
            .Should().Be(new Vector2Int(5, 5));
    }

    [Fact]
    public void Lerp_RoundsToNearestInteger()
    {
        // 0 + (10 - 0) * 0.35 = 3.5 → rounds to 4
        var result = Vector2Int.Lerp(new Vector2Int(0, 0), new Vector2Int(10, 10), 0.35f);
        result.X.Should().Be(4);
        result.Y.Should().Be(4);
    }
}
