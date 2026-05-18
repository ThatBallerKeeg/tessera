using System.Runtime.CompilerServices;

namespace Engine.Core.Procgen;

/// <summary>
/// Stateless 2D noise functions. All methods are deterministic for a given seed,
/// allocation-free, and safe to call from multiple threads simultaneously.
/// Output range for all functions is approximately [-1, 1].
/// </summary>
public static class Noise2D
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Classic 2D Perlin gradient noise.
    /// Uses 8-direction gradients with quintic fade; output range ≈ [-1, 1].
    /// </summary>
    public static float Perlin(float x, float y, int seed)
    {
        int x0 = FloorInt(x), y0 = FloorInt(y);
        int x1 = x0 + 1,      y1 = y0 + 1;
        float fx = x - x0,    fy = y - y0;
        float u  = Fade(fx),  v  = Fade(fy);

        float n00 = Grad2(Hash(x0, y0, seed), fx,     fy);
        float n10 = Grad2(Hash(x1, y0, seed), fx - 1, fy);
        float n01 = Grad2(Hash(x0, y1, seed), fx,     fy - 1);
        float n11 = Grad2(Hash(x1, y1, seed), fx - 1, fy - 1);

        return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v);
    }

    /// <summary>
    /// 2D Simplex noise (Gustavson 2012).
    /// Output range ≈ [-1, 1].
    /// </summary>
    public static float Simplex(float x, float y, int seed)
    {
        // Skew / unskew constants for 2D simplex
        const float F2 = 0.3660254f;  // (√3 − 1) / 2
        const float G2 = 0.2113249f;  // (3 − √3) / 6

        float s  = (x + y) * F2;
        int   i  = FloorInt(x + s);
        int   j  = FloorInt(y + s);
        float t  = (i + j) * G2;

        // Corner 0 relative coordinates
        float x0 = x - (i - t);
        float y0 = y - (j - t);

        // Which simplex triangle?
        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }

        // Corners 1 and 2 relative coordinates
        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + 2f * G2;
        float y2 = y0 - 1f + 2f * G2;

        float n0 = SimplexCorner(Hash(i,      j,      seed), x0, y0);
        float n1 = SimplexCorner(Hash(i + i1, j + j1, seed), x1, y1);
        float n2 = SimplexCorner(Hash(i + 1,  j + 1,  seed), x2, y2);

        // Scale factor 70 maps the unscaled maximum to ≈ 1.
        return 70f * (n0 + n1 + n2);
    }

    /// <summary>
    /// Fractal Brownian Motion: accumulates <paramref name="octaves"/> octaves of
    /// <paramref name="noiseFunc"/>, doubling frequency and halving amplitude each step.
    /// Output is normalised to the same approximate range as the underlying noise.
    /// </summary>
    public static float FBm(
        Func<float, float, int, float> noiseFunc,
        float x, float y, int seed,
        int   octaves,
        float persistence = 0.5f,
        float lacunarity  = 2.0f)
    {
        float value     = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue  = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value    += noiseFunc(x * frequency, y * frequency, seed) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxValue > 0f ? value / maxValue : 0f;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SimplexCorner(int hash, float x, float y)
    {
        float t = 0.5f - x * x - y * y;
        if (t < 0f) return 0f;
        t *= t;
        return t * t * Grad2(hash, x, y);
    }

    // Quintic fade: 6t⁵ − 15t⁴ + 10t³  (C² at 0 and 1, removes grid artefacts)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    // 8-direction gradient: projects (x,y) onto one of 8 diagonal/axis unit vectors.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Grad2(int hash, float x, float y)
    {
        int   h = hash & 7;
        float u = h < 4 ? x : y;
        float v = h < 4 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    // Integer hash with good avalanche — no allocation, no lookup table.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed;
            h ^= x * unchecked((int)0x8DA6B343);
            h ^= y * unchecked((int)0xD8163841);
            h ^= h >> 16;
            h *= 0x7f4a7c15;
            h ^= h >> 15;
            return h;
        }
    }

    // Correct floor for negative floats (C# cast truncates toward zero).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FloorInt(float x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }
}
