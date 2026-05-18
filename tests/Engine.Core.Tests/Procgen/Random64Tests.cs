using AwesomeAssertions;
using Engine.Core.Procgen;

namespace Engine.Core.Tests.Procgen;

public class Random64Tests
{
    // ── Reference sequence ────────────────────────────────────────────────────
    // First 100 NextUInt64() outputs for seed 42, derived from the xoshiro256**
    // reference C implementation (Blackman & Vigna, 2019) using splitmix64 seeding.
    private static readonly ulong[] Seed42Expected =
    [
        0x15780B2E0C2EC716UL, 0x6104D9866D113A7EUL, 0xAE17533239E499A1UL, 0xECB8AD4703B360A1UL,
        0xFDE6DC7FE2EC5E64UL, 0xC50DA53101795238UL, 0xB82154855A65DDB2UL, 0xD99A2743EBE60087UL,
        0xC2E96E726E97647EUL, 0x9556615F775FBC3DUL, 0xAEB53B340C103971UL, 0x4A69DB9873AF8965UL,
        0xCD0FEDA93006C6B6UL, 0x52480865A4B42742UL, 0xB60DEC3BF2D887CDUL, 0xE0B55A68B96677FAUL,
        0x9DE4159EDA9CEF95UL, 0xD9F4B354EC3844D4UL, 0xB5215F43ED431A77UL, 0xB5344CBE421F4F3AUL,
        0x17C5AD539DBB98D9UL, 0x2DD4705AABA5DE2BUL, 0x6FAA904A94C529BDUL, 0x9A1DA25458817417UL,
        0x5061938DA99C7AF0UL, 0x7D3BABC0D1E23440UL, 0x6624536F5AD584D4UL, 0xCA03E50015C044B8UL,
        0xA293144F4F3BD3FAUL, 0x3B38BD77133B0BDAUL, 0x6A0DA881492D3BFDUL, 0x9F6B51D30D502B3AUL,
        0xDCF83AB9A2B09168UL, 0xF1DBBB3E7CAF8512UL, 0xD06FA2C515268D8AUL, 0xBF3B601241D6460CUL,
        0xC8DAC160A4CF65B7UL, 0x0B79E57DE69E68A1UL, 0x77FFE08AAFFCA9F2UL, 0xF8DAE1DEEB08090BUL,
        0x896C10E1F50E7C45UL, 0xB35F3C33364236ADUL, 0xCDB713A2484ABA0DUL, 0xD17557EE842FC622UL,
        0xE5FA6D9F51A65BE7UL, 0x202A8F768818EB71UL, 0x90A2B65696578132UL, 0x8DE344CFE2C7F797UL,
        0xDB73C7B4D941A5A9UL, 0xD3E1718BF28E10A9UL, 0x850B3263A0953DBBUL, 0x51466FD43F32A0ECUL,
        0x3130EB9B89D02158UL, 0xA4D4D91162B2D044UL, 0x0752374EA697B934UL, 0x5BB7058B670DA327UL,
        0x91BE7D3D72CEC5D7UL, 0xC687F6037DE59E9CUL, 0x81DBD737AE287209UL, 0x9EB080FC911EAD60UL,
        0xF3759893228A56ECUL, 0xF18B1A75D5C9A1ABUL, 0x3818CA12DC164711UL, 0xC990D448A6CC309EUL,
        0x125C1354BB1738F2UL, 0x2C0162BA54980A8DUL, 0x3007507A09E5A9E8UL, 0x86CB63BC4DC28E27UL,
        0x72BD872B6D8C758EUL, 0x27211A80821E12BBUL, 0xBD55D53E3430D717UL, 0x7654EB76F9F35787UL,
        0xB7CD97326B1C1D60UL, 0x960D9179DD9A26ECUL, 0xAEDCB86BD40DE374UL, 0x52D6A585752FE880UL,
        0x3F87A8431BBF0CE0UL, 0x12A3BC1C89B77769UL, 0x16BDABC6B717ACAAUL, 0xF33828DEAC2D5480UL,
        0x65CA25A4EC473F38UL, 0x74BE684381B74B7DUL, 0xA17D2FDFA67EF56CUL, 0x8424FC639BB2EB06UL,
        0x57473424A58D2F87UL, 0x924F001E46054938UL, 0x8A68477326EB6C36UL, 0xCC2662D38C9F1EA0UL,
        0x1941236F749BF9D7UL, 0x8EAF25678092DF83UL, 0x3707209FBA6EB65CUL, 0xB9477FABA1E4CAACUL,
        0x6F78B7F02CFAA758UL, 0x38F2B3F537242324UL, 0xFA5EC4DB9B08A90BUL, 0xCC1ECB026638BEFFUL,
        0xD0F1172CEDA99AB5UL, 0x56C0BC947B94EE59UL, 0xF6424747FB5ED934UL, 0x0E67446E763165CDUL,
    ];

    // ── Reference-vector tests ─────────────────────────────────────────────────

    [Fact]
    public void NextUInt64_Seed42_MatchesReferenceSequence()
    {
        var rng = new Random64(42);
        for (int i = 0; i < Seed42Expected.Length; i++)
            rng.NextUInt64().Should().Be(Seed42Expected[i], $"output {i} should match reference");
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new Random64(12345);
        var b = new Random64(12345);
        for (int i = 0; i < 64; i++)
            a.NextUInt64().Should().Be(b.NextUInt64(), $"outputs must match at step {i}");
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new Random64(1);
        var b = new Random64(2);
        bool anyDiff = false;
        for (int i = 0; i < 64; i++)
            if (a.NextUInt64() != b.NextUInt64()) { anyDiff = true; break; }
        anyDiff.Should().BeTrue("different seeds must diverge within 64 steps");
    }

    // ── NextUInt32 / NextInt32 ────────────────────────────────────────────────

    [Fact]
    public void NextUInt32_ReturnsHighHalfOfUInt64()
    {
        // The first raw uint64 from seed 42 is 0x15780B2E0C2EC716.
        // High 32 bits = 0x15780B2E.
        var rng = new Random64(42);
        rng.NextUInt32().Should().Be(0x15780B2EU);
    }

    [Fact]
    public void NextInt32_IsAlwaysNonNegative()
    {
        var rng = new Random64(999);
        for (int i = 0; i < 10_000; i++)
            rng.NextInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Floating-point ────────────────────────────────────────────────────────

    [Fact]
    public void NextDouble_IsInUnitRange()
    {
        var rng = new Random64(7);
        for (int i = 0; i < 10_000; i++)
        {
            double v = rng.NextDouble();
            v.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThan(1.0);
        }
    }

    [Fact]
    public void NextFloat_IsInUnitRange()
    {
        var rng = new Random64(7);
        for (int i = 0; i < 10_000; i++)
        {
            float v = rng.NextFloat();
            v.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThan(1f);
        }
    }

    // ── NextInt range ─────────────────────────────────────────────────────────

    [Fact]
    public void NextInt_StaysWithinRange()
    {
        var rng = new Random64(100);
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.NextInt(-5, 5);
            v.Should().BeGreaterThanOrEqualTo(-5).And.BeLessThan(5);
        }
    }

    [Fact]
    public void NextInt_CoversFullRange()
    {
        var rng = new Random64(100);
        var seen = new HashSet<int>();
        for (int i = 0; i < 100_000; i++)
            seen.Add(rng.NextInt(0, 10));
        seen.Should().HaveCount(10, "all values 0-9 should appear in 100k samples");
    }

    [Fact]
    public void NextInt_InvalidRange_Throws()
    {
        var rng = new Random64(1);
        Action act = () => rng.NextInt(5, 5);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NextFloat_RangedStaysWithinBounds()
    {
        var rng = new Random64(77);
        for (int i = 0; i < 10_000; i++)
        {
            float v = rng.NextFloat(-3f, 7f);
            v.Should().BeGreaterThanOrEqualTo(-3f).And.BeLessThan(7f);
        }
    }

    // ── NextUnitVector ────────────────────────────────────────────────────────

    [Fact]
    public void NextUnitVector_LengthIsOne()
    {
        var rng = new Random64(55);
        for (int i = 0; i < 1_000; i++)
        {
            var v = rng.NextUnitVector();
            float len = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
            len.Should().BeApproximately(1f, 1e-5f, $"unit vector {i} must have length 1");
        }
    }

    // ── NextInsideUnitCircle ──────────────────────────────────────────────────

    [Fact]
    public void NextInsideUnitCircle_LengthAtMostOne()
    {
        var rng = new Random64(66);
        for (int i = 0; i < 1_000; i++)
        {
            var v = rng.NextInsideUnitCircle();
            float lenSq = v.X * v.X + v.Y * v.Y;
            lenSq.Should().BeLessThanOrEqualTo(1f, $"point {i} must be inside unit disk");
        }
    }

    // ── Pick ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Pick_ReturnsElementFromList()
    {
        var rng = new Random64(11);
        var list = new[] { "a", "b", "c", "d" };
        for (int i = 0; i < 1_000; i++)
            list.Should().Contain(rng.Pick(list));
    }

    [Fact]
    public void Pick_EmptyList_Throws()
    {
        var rng = new Random64(1);
        Action act = () => rng.Pick(Array.Empty<int>());
        act.Should().Throw<ArgumentException>();
    }

    // ── Shuffle ───────────────────────────────────────────────────────────────

    [Fact]
    public void Shuffle_ContainsSameElements()
    {
        var rng = new Random64(22);
        var original = Enumerable.Range(0, 20).ToList();
        var shuffled = Enumerable.Range(0, 20).ToList();
        rng.Shuffle(shuffled);
        shuffled.Should().BeEquivalentTo(original, "shuffle preserves all elements");
    }

    [Fact]
    public void Shuffle_IsDeterministic()
    {
        var a = Enumerable.Range(0, 20).ToList();
        var b = Enumerable.Range(0, 20).ToList();
        new Random64(33).Shuffle(a);
        new Random64(33).Shuffle(b);
        a.Should().Equal(b, "same seed must produce same shuffle");
    }

    [Fact]
    public void Shuffle_ActuallyReorders()
    {
        var rng = new Random64(44);
        var list = Enumerable.Range(0, 100).ToList();
        var copy = list.ToList();
        rng.Shuffle(list);
        list.Should().NotEqual(copy, "a shuffle of 100 elements is astronomically unlikely to be identity");
    }
}
