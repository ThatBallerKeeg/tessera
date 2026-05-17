using Engine.Core.Math;
using AwesomeAssertions;

namespace Engine.Core.Tests.Math;

public class Vector2Tests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsComponents()
    {
        var v = new Vector2(3f, 4f);
        v.X.Should().Be(3f);
        v.Y.Should().Be(4f);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_HasZeroComponents()
    {
        Vector2.Zero.X.Should().Be(0f);
        Vector2.Zero.Y.Should().Be(0f);
    }

    [Fact]
    public void One_HasOneComponents()
    {
        Vector2.One.X.Should().Be(1f);
        Vector2.One.Y.Should().Be(1f);
    }

    // ── Arithmetic operators ──────────────────────────────────────────────────

    [Fact]
    public void Addition_ReturnsComponentwiseSum()
    {
        var result = new Vector2(1f, 2f) + new Vector2(3f, 4f);
        result.Should().Be(new Vector2(4f, 6f));
    }

    [Fact]
    public void Subtraction_ReturnsComponentwiseDifference()
    {
        var result = new Vector2(5f, 6f) - new Vector2(2f, 1f);
        result.Should().Be(new Vector2(3f, 5f));
    }

    [Fact]
    public void UnaryNegation_NegatesComponents()
    {
        var result = -new Vector2(1f, -2f);
        result.Should().Be(new Vector2(-1f, 2f));
    }

    [Fact]
    public void MultiplicationByScalar_ScalesComponents()
    {
        var result = new Vector2(2f, 3f) * 4f;
        result.Should().Be(new Vector2(8f, 12f));
    }

    [Fact]
    public void MultiplicationByScalar_IsCommutative()
    {
        var v = new Vector2(2f, 3f);
        (4f * v).Should().Be(v * 4f);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_IsTrue()
    {
        var a = new Vector2(1f, 2f);
        var b = new Vector2(1f, 2f);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_DifferentValues_IsTrue()
    {
        (new Vector2(1f, 2f) != new Vector2(1f, 3f)).Should().BeTrue();
    }

    [Fact]
    public void Equals_BoxedValue_WorksViaObjectOverride()
    {
        var a = new Vector2(1f, 2f);
        object b = new Vector2(1f, 2f);
        a.Equals(b).Should().BeTrue();
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_EqualValues_SameHash()
    {
        new Vector2(1f, 2f).GetHashCode().Should().Be(new Vector2(1f, 2f).GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_DifferentHash()
    {
        new Vector2(1f, 2f).GetHashCode().Should().NotBe(new Vector2(2f, 1f).GetHashCode());
    }

    // ── Dot ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Dot_PerpendicularVectors_ReturnsZero()
    {
        Vector2.Dot(new Vector2(1f, 0f), new Vector2(0f, 1f)).Should().Be(0f);
    }

    [Fact]
    public void Dot_ParallelVectors_ReturnsSquaredLength()
    {
        Vector2.Dot(new Vector2(3f, 4f), new Vector2(3f, 4f)).Should().Be(25f);
    }

    [Fact]
    public void Dot_ArbitraryVectors_ReturnsExpected()
    {
        Vector2.Dot(new Vector2(1f, 2f), new Vector2(3f, 4f)).Should().Be(11f);
    }

    // ── Distance ─────────────────────────────────────────────────────────────

    [Fact]
    public void Distance_KnownTriangle_Returns5()
    {
        Vector2.Distance(Vector2.Zero, new Vector2(3f, 4f)).Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void Distance_SamePoint_ReturnsZero()
    {
        Vector2.Distance(new Vector2(2f, 3f), new Vector2(2f, 3f)).Should().Be(0f);
    }

    [Fact]
    public void Distance_IsSymmetric()
    {
        var a = new Vector2(1f, 2f);
        var b = new Vector2(4f, 6f);
        Vector2.Distance(a, b).Should().BeApproximately(Vector2.Distance(b, a), 1e-6f);
    }

    // ── Lerp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_AtZero_ReturnsA()
    {
        Vector2.Lerp(new Vector2(1f, 2f), new Vector2(9f, 8f), 0f).Should().Be(new Vector2(1f, 2f));
    }

    [Fact]
    public void Lerp_AtOne_ReturnsB()
    {
        Vector2.Lerp(new Vector2(1f, 2f), new Vector2(9f, 8f), 1f).Should().Be(new Vector2(9f, 8f));
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var result = Vector2.Lerp(new Vector2(0f, 0f), new Vector2(10f, 10f), 0.5f);
        result.X.Should().BeApproximately(5f, 1e-5f);
        result.Y.Should().BeApproximately(5f, 1e-5f);
    }
}
