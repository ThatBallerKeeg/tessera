using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Procgen;

namespace Engine.Core.Tests.Procgen;

public class PoissonTests
{
    // ── Min-distance invariant (load-bearing) ─────────────────────────────────

    /// <summary>
    /// O(n²) check — acceptable for ~260 points in a 100×100 field.
    /// This is the core correctness guarantee of Poisson-disk sampling.
    /// </summary>
    [Fact]
    public void Sample2D_SmallField_AllPairsRespectMinDistance()
    {
        var rng     = new Random64(42);
        float d     = 5f;
        var points  = Poisson.Sample2D(100f, 100f, d, rng);

        float minDistSq = d * d;
        for (int i = 0; i < points.Count; i++)
        for (int j = i + 1; j < points.Count; j++)
        {
            float dx = points[i].X - points[j].X;
            float dy = points[i].Y - points[j].Y;
            (dx * dx + dy * dy).Should().BeGreaterThanOrEqualTo(minDistSq - 1e-4f,
                $"points[{i}] and points[{j}] are closer than minDistance {d}");
        }
    }

    /// <summary>
    /// Grid-accelerated O(n·25) check for a larger field (~6 300 points).
    /// Rebuilds the same cell-grid used by the sampler to make neighbour
    /// lookup O(1) per point instead of O(n).
    /// </summary>
    [Fact]
    public void Sample2D_LargeField_AllPairsRespectMinDistance_GridAccelerated()
    {
        var rng    = new Random64(99);
        float w    = 300f, h = 300f, d = 3f;
        var points = Poisson.Sample2D(w, h, d, rng);

        // Build a multi-point verification grid (cells may overlap during verify).
        float cellSize = d / MathF.Sqrt(2f);
        int gridW = (int)MathF.Ceiling(w / cellSize) + 1;
        int gridH = (int)MathF.Ceiling(h / cellSize) + 1;

        var cells = new List<int>[gridW * gridH];
        for (int i = 0; i < cells.Length; i++) cells[i] = [];

        for (int i = 0; i < points.Count; i++)
        {
            int cx = (int)(points[i].X / cellSize);
            int cy = (int)(points[i].Y / cellSize);
            cells[cy * gridW + cx].Add(i);
        }

        float minDistSq = d * d - 1e-4f;
        for (int i = 0; i < points.Count; i++)
        {
            int cx = (int)(points[i].X / cellSize);
            int cy = (int)(points[i].Y / cellSize);
            int x0 = System.Math.Max(0, cx - 2), x1 = System.Math.Min(gridW - 1, cx + 2);
            int y0 = System.Math.Max(0, cy - 2), y1 = System.Math.Min(gridH - 1, cy + 2);

            for (int gy = y0; gy <= y1; gy++)
            for (int gx = x0; gx <= x1; gx++)
            {
                foreach (int j in cells[gy * gridW + gx])
                {
                    if (j <= i) continue;   // each pair checked once
                    float dx = points[i].X - points[j].X;
                    float dy = points[i].Y - points[j].Y;
                    (dx * dx + dy * dy).Should().BeGreaterThanOrEqualTo(minDistSq,
                        $"points[{i}] and points[{j}] are closer than minDistance {d}");
                }
            }
        }
    }

    // ── Coverage density ──────────────────────────────────────────────────────
    // Bridson's algorithm empirically achieves ~55% of the hexagonal packing maximum.
    // The test uses a [45%, 100%] window to accommodate seed-to-seed variation.
    // Hex packing max = area × 2 / (√3 × minDist²).

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    public void Sample2D_CoverageDensity_InExpectedRange(int seed)
    {
        float w    = 100f, h = 100f, d = 5f;
        var points = Poisson.Sample2D(w, h, d, new Random64((ulong)seed));

        float hexMax = w * h * 2f / (MathF.Sqrt(3f) * d * d);
        int   lo     = (int)(hexMax * 0.45f);
        int   hi     = (int)MathF.Ceiling(hexMax);

        points.Count.Should().BeGreaterThanOrEqualTo(lo,
            $"expected ≥ {lo} points (45%% of hex-packing max {hexMax:F0})");
        points.Count.Should().BeLessThanOrEqualTo(hi,
            $"expected ≤ {hi} points (hex-packing max)");
    }

    // ── Supporting checks ─────────────────────────────────────────────────────

    [Fact]
    public void Sample2D_AllPointsWithinDomainBounds()
    {
        var rng    = new Random64(1);
        var points = Poisson.Sample2D(80f, 60f, 4f, rng);
        foreach (var p in points)
        {
            p.X.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThan(80f);
            p.Y.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThan(60f);
        }
    }

    [Fact]
    public void Sample2D_SameSeed_ProducesSamePoints()
    {
        var a = Poisson.Sample2D(50f, 50f, 5f, new Random64(123));
        var b = Poisson.Sample2D(50f, 50f, 5f, new Random64(123));

        a.Count.Should().Be(b.Count, "same seed must yield same count");
        for (int i = 0; i < a.Count; i++)
        {
            a[i].X.Should().Be(b[i].X, $"point[{i}].X must be identical");
            a[i].Y.Should().Be(b[i].Y, $"point[{i}].Y must be identical");
        }
    }

    [Fact]
    public void Sample2D_ReturnsAtLeastOnePoint()
    {
        var points = Poisson.Sample2D(10f, 10f, 1f, new Random64(5));
        points.Should().NotBeEmpty();
    }
}
