using Engine.Core.Math;

namespace Engine.Core.Procgen;

/// <summary>
/// Poisson-disk sampling using Bridson's algorithm (2007).
/// Every returned point is at least <c>minDistance</c> from every other point.
/// Grid acceleration: cell size = minDistance / √2 so that checking the 5×5
/// neighbourhood (±2 cells in each direction) covers the full exclusion disk.
/// </summary>
public static class Poisson
{
    /// <summary>
    /// Generates a Poisson-disk sample inside [0, <paramref name="width"/>) ×
    /// [0, <paramref name="height"/>).
    /// </summary>
    /// <param name="maxAttempts">
    /// Candidate points tried per active sample before it is retired from the
    /// active list. Higher values yield denser results at the cost of runtime.
    /// </param>
    public static List<Vector2> Sample2D(
        float   width,
        float   height,
        float   minDistance,
        Random64 rng,
        int     maxAttempts = 30)
    {
        float cellSize = minDistance / MathF.Sqrt(2f);
        int   gridW    = (int)MathF.Ceiling(width  / cellSize);
        int   gridH    = (int)MathF.Ceiling(height / cellSize);

        // grid[cy * gridW + cx] = index into `points`, or -1 when empty.
        // Bridson's invariant: each cell contains at most one point.
        int[] grid = new int[gridW * gridH];
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;

        var points = new List<Vector2>();
        var active = new List<int>();   // indices into `points`

        // Seed with one random point anywhere in the domain.
        GridInsert(
            new Vector2(rng.NextFloat(0f, width), rng.NextFloat(0f, height)),
            points, active, grid, gridW, cellSize);

        while (active.Count > 0)
        {
            int refSlot = rng.NextInt(0, active.Count);
            var refPt   = points[active[refSlot]];
            bool placed = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Uniform random point in the annulus [minDistance, 2·minDistance).
                float angle  = rng.NextFloat() * MathF.PI * 2f;
                float radius = minDistance * (1f + rng.NextFloat());
                var   cand   = new Vector2(
                    refPt.X + radius * MathF.Cos(angle),
                    refPt.Y + radius * MathF.Sin(angle));

                if (cand.X < 0f || cand.X >= width ||
                    cand.Y < 0f || cand.Y >= height)
                    continue;

                if (!TooClose(cand, points, grid, gridW, gridH, cellSize, minDistance))
                {
                    GridInsert(cand, points, active, grid, gridW, cellSize);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // Swap-and-pop: O(1) removal from unordered active list.
                active[refSlot] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }
        }

        return points;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void GridInsert(
        Vector2 pt, List<Vector2> points, List<int> active,
        int[] grid, int gridW, float cellSize)
    {
        int idx = points.Count;
        points.Add(pt);
        active.Add(idx);
        int cx = (int)(pt.X / cellSize);
        int cy = (int)(pt.Y / cellSize);
        grid[cy * gridW + cx] = idx;
    }

    private static bool TooClose(
        Vector2 cand, List<Vector2> points,
        int[] grid, int gridW, int gridH,
        float cellSize, float minDist)
    {
        int   cx   = (int)(cand.X / cellSize);
        int   cy   = (int)(cand.Y / cellSize);
        float dSq  = minDist * minDist;

        int x0 = System.Math.Max(0, cx - 2), x1 = System.Math.Min(gridW - 1, cx + 2);
        int y0 = System.Math.Max(0, cy - 2), y1 = System.Math.Min(gridH - 1, cy + 2);

        for (int gy = y0; gy <= y1; gy++)
        for (int gx = x0; gx <= x1; gx++)
        {
            int idx = grid[gy * gridW + gx];
            if (idx < 0) continue;
            var  p  = points[idx];
            float dx = cand.X - p.X;
            float dy = cand.Y - p.Y;
            if (dx * dx + dy * dy < dSq) return true;
        }
        return false;
    }
}
