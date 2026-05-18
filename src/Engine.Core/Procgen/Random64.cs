using Engine.Core.Math;

namespace Engine.Core.Procgen;

/// <summary>
/// Seedable pseudo-random number generator using the xoshiro256** algorithm
/// (Blackman &amp; Vigna, 2019). State is initialized from a single seed via splitmix64.
/// Not thread-safe — use one instance per system that requires determinism.
/// </summary>
public sealed class Random64
{
    private ulong _s0, _s1, _s2, _s3;

    /// <param name="seed">Arbitrary 64-bit seed. Different seeds produce independent sequences.</param>
    public Random64(ulong seed)
    {
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    // ── Core generator ────────────────────────────────────────────────────────

    /// <summary>Returns the next raw 64-bit output and advances the state.</summary>
    public ulong NextUInt64()
    {
        ulong result = RotL(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotL(_s3, 45);

        return result;
    }

    /// <summary>Returns the high 32 bits of the next 64-bit output.</summary>
    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>Returns a non-negative random int (range 0 to <see cref="int.MaxValue"/>).</summary>
    public int NextInt32() => (int)(NextUInt64() >> 33);

    // ── Floating-point ────────────────────────────────────────────────────────

    /// <summary>Returns a uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Returns a uniform float in [0, 1).</summary>
    public float NextFloat() => (float)NextDouble();

    // ── Ranged ───────────────────────────────────────────────────────────────

    /// <summary>Returns a uniform int in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
            throw new ArgumentException($"minInclusive ({minInclusive}) must be less than maxExclusive ({maxExclusive}).");

        uint range = (uint)(maxExclusive - minInclusive);
        // Debiased rejection sampling: discard values below the modulo bias threshold.
        uint threshold = unchecked((uint)(-(int)range)) % range;
        uint u;
        do { u = NextUInt32(); } while (u < threshold);
        return (int)(u % range) + minInclusive;
    }

    /// <summary>Returns a uniform float in [<paramref name="min"/>, <paramref name="max"/>).</summary>
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    // ── Geometric ────────────────────────────────────────────────────────────

    /// <summary>Returns a random unit vector (uniform on the unit circle).</summary>
    public Vector2 NextUnitVector()
    {
        float angle = NextFloat() * MathF.PI * 2f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    /// <summary>Returns a random point uniformly distributed inside the unit disk.</summary>
    public Vector2 NextInsideUnitCircle()
    {
        Vector2 p;
        do { p = new Vector2(NextFloat(-1f, 1f), NextFloat(-1f, 1f)); }
        while (p.X * p.X + p.Y * p.Y > 1f);
        return p;
    }

    // ── Collection ───────────────────────────────────────────────────────────

    /// <summary>Returns a uniformly random element from <paramref name="list"/>.</summary>
    public T Pick<T>(IReadOnlyList<T> list)
    {
        if (list.Count == 0) throw new ArgumentException("Cannot pick from an empty list.");
        return list[NextInt(0, list.Count)];
    }

    /// <summary>Shuffles <paramref name="list"/> in-place using Fisher-Yates.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));

    private static ulong SplitMix64(ref ulong x)
    {
        ulong z = (x += 0x9e3779b97f4a7c15UL);
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
        return z ^ (z >> 31);
    }
}
