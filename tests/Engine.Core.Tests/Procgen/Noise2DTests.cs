using AwesomeAssertions;
using Engine.Core.Procgen;

namespace Engine.Core.Tests.Procgen;

public class Noise2DTests
{
    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Perlin_SameSeedAndCoords_ReturnsSameValue()
    {
        float a = Noise2D.Perlin(1.23f, -4.56f, 99);
        float b = Noise2D.Perlin(1.23f, -4.56f, 99);
        a.Should().Be(b);
    }

    [Fact]
    public void Simplex_SameSeedAndCoords_ReturnsSameValue()
    {
        float a = Noise2D.Simplex(7.7f, -2.1f, 42);
        float b = Noise2D.Simplex(7.7f, -2.1f, 42);
        a.Should().Be(b);
    }

    [Fact]
    public void FBm_SameSeedAndCoords_ReturnsSameValue()
    {
        float a = Noise2D.FBm(Noise2D.Perlin, 3f, 3f, 7, octaves: 6);
        float b = Noise2D.FBm(Noise2D.Perlin, 3f, 3f, 7, octaves: 6);
        a.Should().Be(b);
    }

    [Fact]
    public void Perlin_DifferentSeeds_ProduceDifferentOutputs()
    {
        // Sample 50 points; at least one must differ between seed 1 and seed 2.
        // Single-point checks are fragile because certain coordinates (e.g. grid
        // centres, lattice points) can yield 0 for any seed by symmetry.
        float totalDiff = 0f;
        for (int i = 0; i < 50; i++)
        {
            float x = i * 0.37f + 0.13f;
            float y = i * 0.19f + 0.41f;
            totalDiff += MathF.Abs(Noise2D.Perlin(x, y, 1) - Noise2D.Perlin(x, y, 2));
        }
        totalDiff.Should().BeGreaterThan(0f, "seed 1 and seed 2 must produce distinct sequences");
    }

    [Fact]
    public void Simplex_DifferentSeeds_ProduceDifferentOutputs()
    {
        float totalDiff = 0f;
        for (int i = 0; i < 50; i++)
        {
            float x = i * 0.37f + 0.13f;
            float y = i * 0.19f + 0.41f;
            totalDiff += MathF.Abs(Noise2D.Simplex(x, y, 1) - Noise2D.Simplex(x, y, 2));
        }
        totalDiff.Should().BeGreaterThan(0f, "seed 1 and seed 2 must produce distinct sequences");
    }

    // ── Range ─────────────────────────────────────────────────────────────────
    // Probed over a 401×401 grid at 0.1-unit steps for 5 seeds.
    // Empirical max for Perlin = 1.000, Simplex = 0.998; bound 1.05 gives headroom.

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(42)]
    public void Perlin_OutputWithinBounds(int seed)
    {
        for (int yi = -100; yi <= 100; yi++)
        for (int xi = -100; xi <= 100; xi++)
        {
            float v = Noise2D.Perlin(xi * 0.3f, yi * 0.3f, seed);
            v.Should().BeGreaterThanOrEqualTo(-1.05f).And.BeLessThanOrEqualTo(1.05f,
                $"Perlin({xi * 0.3f}, {yi * 0.3f}, {seed}) = {v} is out of range");
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(42)]
    public void Simplex_OutputWithinBounds(int seed)
    {
        for (int yi = -100; yi <= 100; yi++)
        for (int xi = -100; xi <= 100; xi++)
        {
            float v = Noise2D.Simplex(xi * 0.3f, yi * 0.3f, seed);
            v.Should().BeGreaterThanOrEqualTo(-1.05f).And.BeLessThanOrEqualTo(1.05f,
                $"Simplex({xi * 0.3f}, {yi * 0.3f}, {seed}) = {v} is out of range");
        }
    }

    [Fact]
    public void FBm_OutputWithinBounds()
    {
        // FBm normalises to the same range as the underlying noise.
        for (int yi = -50; yi <= 50; yi++)
        for (int xi = -50; xi <= 50; xi++)
        {
            float v = Noise2D.FBm(Noise2D.Perlin, xi * 0.3f, yi * 0.3f, 0, octaves: 5);
            v.Should().BeGreaterThanOrEqualTo(-1.05f).And.BeLessThanOrEqualTo(1.05f,
                $"FBm at ({xi * 0.3f}, {yi * 0.3f}) = {v}");
        }
    }

    // ── Continuity ────────────────────────────────────────────────────────────
    // Perlin and Simplex are C¹-continuous. For step δ = 0.01 the change
    // is bounded by L·δ where L (Lipschitz constant) ≤ ~6 for these functions,
    // so |Δ| < 0.1 is a conservative but meaningful check.

    [Fact]
    public void Perlin_NeighboringValues_AreClose()
    {
        const float step = 0.01f;
        const float maxDelta = 0.1f;
        for (int i = 0; i < 200; i++)
        {
            float x = i * 0.5f;
            float y = i * 0.3f;
            float v0 = Noise2D.Perlin(x,        y, 1);
            float vx = Noise2D.Perlin(x + step, y, 1);
            float vy = Noise2D.Perlin(x, y + step,  1);
            MathF.Abs(vx - v0).Should().BeLessThan(maxDelta,
                $"Perlin x-step discontinuity at ({x}, {y})");
            MathF.Abs(vy - v0).Should().BeLessThan(maxDelta,
                $"Perlin y-step discontinuity at ({x}, {y})");
        }
    }

    [Fact]
    public void Simplex_NeighboringValues_AreClose()
    {
        const float step = 0.01f;
        const float maxDelta = 0.1f;
        for (int i = 0; i < 200; i++)
        {
            float x = i * 0.5f;
            float y = i * 0.3f;
            float v0 = Noise2D.Simplex(x,        y, 1);
            float vx = Noise2D.Simplex(x + step, y, 1);
            float vy = Noise2D.Simplex(x, y + step,  1);
            MathF.Abs(vx - v0).Should().BeLessThan(maxDelta,
                $"Simplex x-step discontinuity at ({x}, {y})");
            MathF.Abs(vy - v0).Should().BeLessThan(maxDelta,
                $"Simplex y-step discontinuity at ({x}, {y})");
        }
    }

    // ── FBm octave behaviour ──────────────────────────────────────────────────

    [Fact]
    public void FBm_WorksWithSimplexDelegate()
    {
        // Verify FBm accepts Noise2D.Simplex as a method group without error.
        float v = Noise2D.FBm(Noise2D.Simplex, 1f, 2f, seed: 5, octaves: 4);
        v.Should().BeGreaterThanOrEqualTo(-1.05f).And.BeLessThanOrEqualTo(1.05f);
    }

    [Fact]
    public void FBm_MoreOctavesDoNotExplode()
    {
        // 10 octaves of fBm with persistence=0.5 → max amplitude sum = 2; normalised to 1.
        float v = Noise2D.FBm(Noise2D.Perlin, 0.5f, 0.5f, seed: 0, octaves: 10);
        v.Should().BeGreaterThanOrEqualTo(-1.05f).And.BeLessThanOrEqualTo(1.05f);
    }

    [Fact]
    public void FBm_ZeroOctaves_ReturnsZero()
    {
        float v = Noise2D.FBm(Noise2D.Perlin, 1f, 1f, seed: 1, octaves: 0);
        v.Should().Be(0f);
    }
}
