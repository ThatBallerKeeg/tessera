using System.Diagnostics;
using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Procgen;
using Engine.Core.Tiles;

namespace Engine.Core.Tests.Procgen;

public class ProcGenStressTests
{
    // Fills 500×500 tiles using Perlin noise with the given seed.
    private static void FillFromPerlin(TilemapData tilemap, int seed = 42)
    {
        for (int y = 0; y < 500; y++)
        for (int x = 0; x < 500; x++)
        {
            float v = Noise2D.Perlin(x * 0.05f, y * 0.05f, seed: seed);
            tilemap.SetTile(new Vector2Int(x, y), v > 0 ? new TileId(1) : new TileId(2));
        }
    }

    // ── 1. Bulk-edit perf gate ────────────────────────────────────────────────

    [Fact]
    public void ProcGen_500x500_FromPerlin_BulkEdit_UnderHalfSecond()
    {
        var tilemap = new TilemapData();
        var rng     = new Random64(seed: 42);

        var sw = Stopwatch.StartNew();
        using (tilemap.BeginBulkEdit())
        {
            for (int y = 0; y < 500; y++)
            for (int x = 0; x < 500; x++)
            {
                float v = Noise2D.Perlin(x * 0.05f, y * 0.05f, seed: 42);
                tilemap.SetTile(new Vector2Int(x, y), v > 0 ? new TileId(1) : new TileId(2));
            }
        }
        sw.Stop();

        GC.KeepAlive(rng);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Bulk edit took {sw.ElapsedMilliseconds}ms, budget is 500ms");
    }

    // ── 2. Naive vs bulk-edit event count comparison ──────────────────────────

    [Fact]
    public void ProcGen_NaiveVsBulkEdit_DocumentsPerfRatio()
    {
        // 500×500 = 250,000 tiles; ChunkSize=16 → 32×32 = 1,024 chunks.
        // Naive fires one ChunkDirty per SetTile that changes a tile → 250,000 events.
        // BeginBulkEdit batches dirty marks; on dispose it fires once per unique chunk
        // → 1,024 events. Event ratio = 244:1.
        //
        // Wall-clock ratio without a subscriber doing real work is close to 1:1 because
        // SetTile dominates. With a TilemapRenderer subscriber rebuilding chunk draw-lists
        // on each event, the wall-clock improvement tracks the event ratio (~244×).
        //
        // Measured event ratio (always deterministic): 250,000 / 1,024 = 244.
        const int expectedNaiveEvents = 250_000;
        const int expectedBulkEvents  = 1_024;

        // ── Naive ────────────────────────────────────────────────────────────────
        var naiveMap   = new TilemapData();
        int naiveCount = 0;
        naiveMap.ChunkDirty += _ => naiveCount++;

        var swNaive = Stopwatch.StartNew();
        FillFromPerlin(naiveMap);
        swNaive.Stop();

        // ── Bulk edit ────────────────────────────────────────────────────────────
        var bulkMap   = new TilemapData();
        int bulkCount = 0;
        bulkMap.ChunkDirty += _ => bulkCount++;

        var swBulk = Stopwatch.StartNew();
        using (bulkMap.BeginBulkEdit())
            FillFromPerlin(bulkMap);
        swBulk.Stop();

        // ── Assertions ───────────────────────────────────────────────────────────
        naiveCount.Should().Be(expectedNaiveEvents);
        bulkCount.Should().Be(expectedBulkEvents);
        (naiveCount / bulkCount).Should().BeGreaterThanOrEqualTo(5);

        Console.WriteLine($"Naive: {swNaive.ElapsedMilliseconds}ms ({naiveCount} events)");
        Console.WriteLine($"Bulk:  {swBulk.ElapsedMilliseconds}ms ({bulkCount} events)");
        Console.WriteLine($"Event ratio: {naiveCount}/{bulkCount} = {naiveCount / bulkCount}:1");
    }

    // ── 3. Determinism ───────────────────────────────────────────────────────

    [Fact]
    public void ProcGen_500x500_SameSeed_ProducesByteIdenticalOutput()
    {
        var mapA = new TilemapData();
        var mapB = new TilemapData();

        using (mapA.BeginBulkEdit())
            FillFromPerlin(mapA, seed: 42);

        using (mapB.BeginBulkEdit())
            FillFromPerlin(mapB, seed: 42);

        for (int y = 0; y < 500; y++)
        for (int x = 0; x < 500; x++)
        {
            var pos = new Vector2Int(x, y);
            mapA.GetTile(pos).Should().Be(mapB.GetTile(pos));
        }
    }
}
