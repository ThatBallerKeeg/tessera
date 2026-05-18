namespace Engine.Runtime.Tiles;

/// <summary>
/// Monotonic game-time clock shared across all tilemap layers.
/// Advance once per frame (typically from <c>GameTime.TotalGameTime.TotalMilliseconds</c>).
/// Animated tiles use it to compute their current frame index:
/// <c>frameIndex = (ElapsedMs / frame.DurationMs) % frameCount</c>.
/// </summary>
public readonly struct TilemapClock
{
    /// <summary>Total game time elapsed in milliseconds.</summary>
    public long ElapsedMs { get; }

    /// <summary>Creates a clock at the given elapsed time.</summary>
    public TilemapClock(long elapsedMs) => ElapsedMs = elapsedMs;
}
