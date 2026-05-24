using Engine.Core.Animation;
using Engine.Core.Sprites;
using Engine.Runtime.Entities;

namespace Engine.Runtime.Components;

/// <summary>
/// Drives an <see cref="AnimationClip"/>, advancing frames each
/// <see cref="Component.OnUpdate"/> and exposing <see cref="CurrentSpriteId"/>
/// for a sibling <see cref="SpriteRenderer"/> to consume in <c>OnDraw</c>.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Play"/> to start a clip.  Elapsed time is tracked in milliseconds;
/// each <see cref="OnUpdate"/> call advances it by <c>deltaSeconds × 1000</c>.
/// </para>
/// <para>
/// <see cref="AnimationEvent"/> markers fire via <see cref="EventFired"/> when their
/// <see cref="AnimationEvent.FrameIndex"/> is <em>crossed</em> — including any frames
/// skipped due to a large delta.  For looping clips events re-fire each loop.
/// </para>
/// <para>
/// Non-looping clips stop at the final frame and set <see cref="IsComplete"/>;
/// looping clips run indefinitely, wrapping to frame 0 each cycle.
/// </para>
/// </remarks>
public sealed class Animator : Component
{
    // ── Private playback state ────────────────────────────────────────────────

    private AnimationClip? _clip;
    private float          _elapsedMs;
    private int            _frameIndex;

    // -1 = Play() called but no tick processed yet — causes frame-0 events to
    // fire on the very first OnUpdate, even with a zero delta.
    private int _prevFrameIndex = -1;

    private float   _totalDurationMs;
    private float[] _frameStarts = Array.Empty<float>();

    // ── Public read-only state ────────────────────────────────────────────────

    /// <summary>The clip currently playing, or <see langword="null"/> if none is set.</summary>
    public AnimationClip? CurrentClip => _clip;

    /// <summary>
    /// The <see cref="SpriteId"/> corresponding to the active clip's current frame.
    /// <see cref="SpriteId.Empty"/> when no clip is set or the clip has no frames.
    /// A sibling <see cref="SpriteRenderer"/> reads this in <c>OnDraw</c>.
    /// </summary>
    public SpriteId CurrentSpriteId { get; private set; } = SpriteId.Empty;

    /// <summary>
    /// <see langword="true"/> for non-looping clips once the final frame's full duration
    /// has elapsed.  Always <see langword="false"/> for looping clips.
    /// Resets to <see langword="false"/> on each <see cref="Play"/> call.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>Total duration of the current clip in milliseconds. Zero when no clip is set.</summary>
    public float TotalDurationMs => _totalDurationMs;

    /// <summary>Current elapsed time within the clip in milliseconds.</summary>
    public float ElapsedMs => _elapsedMs;

    /// <summary>Current zero-based frame index within the active clip.</summary>
    public int FrameIndex => _frameIndex;

    /// <summary>
    /// Raised inside <see cref="OnUpdate"/> each time an <see cref="AnimationEvent"/>
    /// marker is crossed.  All events skipped by a large delta fire in list order.
    /// </summary>
    public event Action<AnimationEvent>? EventFired;

    // ── Public control ────────────────────────────────────────────────────────

    /// <summary>
    /// Starts playing <paramref name="clip"/> from frame 0.
    /// <list type="bullet">
    ///   <item>Resets elapsed time to 0.</item>
    ///   <item>Sets <see cref="CurrentSpriteId"/> to frame 0's sprite immediately so
    ///         renderers are correct before the first <see cref="OnUpdate"/>.</item>
    ///   <item>Clears <see cref="IsComplete"/>.</item>
    ///   <item>Resets the event-crossing tracker so frame-0 events fire on the next tick.</item>
    /// </list>
    /// </summary>
    public void Play(AnimationClip clip)
    {
        _clip           = clip;
        _elapsedMs      = 0f;
        _frameIndex     = 0;
        _prevFrameIndex = -1;
        IsComplete      = false;

        BuildCumulativeTable(clip);

        CurrentSpriteId = clip.Frames.Count > 0
            ? clip.Frames[0].SpriteId
            : SpriteId.Empty;
    }

    /// <summary>
    /// Seeks to <paramref name="ms"/> within the current clip <em>without firing events</em>.
    /// Updates <see cref="CurrentSpriteId"/> and <see cref="FrameIndex"/> immediately.
    /// Clamps to [0, <see cref="TotalDurationMs"/>].
    /// No-op when no clip is set.
    /// </summary>
    /// <remarks>
    /// Intended for editor scrubbing.  Game code that needs to jump clips should use
    /// <see cref="Play"/> instead.
    /// </remarks>
    public void SeekMs(float ms)
    {
        if (_clip is null || _totalDurationMs <= 0f) return;
        _elapsedMs      = System.Math.Clamp(ms, 0f, _totalDurationMs);
        _frameIndex     = ComputeFrameIndex(_elapsedMs);
        CurrentSpriteId = _clip.Frames[_frameIndex].SpriteId;
        _prevFrameIndex = _frameIndex;
        IsComplete      = false;
    }

    /// <summary>
    /// Seeks to the start of the next frame without firing events.
    /// On a looping clip wraps from the last frame back to frame 0.
    /// On a non-looping clip clamps at the last frame.
    /// No-op when no clip is set.
    /// </summary>
    public void StepForward()
    {
        if (_clip is null || _clip.Frames.Count == 0) return;
        int last = _clip.Frames.Count - 1;
        int next = _frameIndex < last ? _frameIndex + 1
                 : _clip.Loops        ? 0
                                      : last;
        SeekMs(_frameStarts[next]);
    }

    /// <summary>
    /// Rebuilds the cumulative frame-duration table from the current clip without
    /// resetting <see cref="ElapsedMs"/>.  Call after editing frame durations mid-playback
    /// so <see cref="OnUpdate"/> uses the new values immediately.
    /// Clamps elapsed to the new total duration and recomputes <see cref="FrameIndex"/>.
    /// No-op when no clip is set.
    /// </summary>
    public void ResyncClip()
    {
        if (_clip is null || _clip.Frames.Count == 0) return;
        BuildCumulativeTable(_clip);
        if (_totalDurationMs > 0f)
        {
            _elapsedMs  = _clip.Loops
                ? _elapsedMs % _totalDurationMs
                : System.Math.Clamp(_elapsedMs, 0f, _totalDurationMs);
            _frameIndex     = ComputeFrameIndex(_elapsedMs);
            CurrentSpriteId = _clip.Frames[_frameIndex].SpriteId;
            _prevFrameIndex = _frameIndex;
        }
    }

    /// <summary>
    /// Seeks to the start of the previous frame without firing events.
    /// On a looping clip wraps from frame 0 back to the last frame.
    /// On a non-looping clip clamps at frame 0.
    /// No-op when no clip is set.
    /// </summary>
    public void StepBack()
    {
        if (_clip is null || _clip.Frames.Count == 0) return;
        int last = _clip.Frames.Count - 1;
        int prev = _frameIndex > 0 ? _frameIndex - 1
                 : _clip.Loops     ? last
                                   : 0;
        SeekMs(_frameStarts[prev]);
    }

    // ── Component lifecycle ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void OnUpdate(float deltaSeconds)
    {
        if (_clip is null || IsComplete) return;
        if (_clip.Frames.Count == 0 || _totalDurationMs <= 0f) return;

        int   prevIdx     = _prevFrameIndex;
        int   lastIdx     = _clip.Frames.Count - 1;
        float prevElapsed = _elapsedMs;

        _elapsedMs += deltaSeconds * 1000f;

        bool wrapped = false;

        if (!_clip.Loops)
        {
            if (_elapsedMs >= _totalDurationMs)
            {
                _elapsedMs  = _totalDurationMs;
                _frameIndex = lastIdx;
                IsComplete  = true;
            }
            else
            {
                _frameIndex = ComputeFrameIndex(_elapsedMs);
            }
        }
        else
        {
            // Detect a loop-boundary crossing before applying modulo so we can
            // fire events on both the tail of the old cycle and the head of the new one.
            if (prevElapsed < _totalDurationMs && _elapsedMs >= _totalDurationMs)
                wrapped = true;

            _elapsedMs  = _elapsedMs % _totalDurationMs;
            _frameIndex = ComputeFrameIndex(_elapsedMs);
        }

        CurrentSpriteId = _clip.Frames[_frameIndex].SpriteId;
        FireEventsInRange(prevIdx, _frameIndex, wrapped, lastIdx);
        _prevFrameIndex = _frameIndex;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pre-computes per-frame start times (cumulative sum of <c>DurationMs</c>)
    /// and the total clip duration.  Called once per <see cref="Play"/>.
    /// </summary>
    private void BuildCumulativeTable(AnimationClip clip)
    {
        int count = clip.Frames.Count;
        if (_frameStarts.Length != count)
            _frameStarts = new float[count];

        float cum = 0f;
        for (int i = 0; i < count; i++)
        {
            _frameStarts[i] = cum;
            cum += clip.Frames[i].DurationMs;
        }
        _totalDurationMs = cum;
    }

    /// <summary>
    /// Returns the index of the last frame whose start time is ≤ <paramref name="posMs"/>.
    /// Scans backwards; correct and allocation-free for short frame lists.
    /// </summary>
    private int ComputeFrameIndex(float posMs)
    {
        int last = _clip!.Frames.Count - 1;
        for (int i = last; i > 0; i--)
        {
            if (posMs >= _frameStarts[i]) return i;
        }
        return 0;
    }

    /// <summary>
    /// Raises <see cref="EventFired"/> for every <see cref="AnimationEvent"/> whose
    /// <see cref="AnimationEvent.FrameIndex"/> falls in the crossed range.
    /// <list type="bullet">
    ///   <item>No wrap: (<paramref name="prevIdx"/>, <paramref name="curIdx"/>]</item>
    ///   <item>Wrap:    (<paramref name="prevIdx"/>, <paramref name="lastIdx"/>] ∪ [0, <paramref name="curIdx"/>]</item>
    /// </list>
    /// </summary>
    private void FireEventsInRange(int prevIdx, int curIdx, bool wrapped, int lastIdx)
    {
        if (_clip is null) return;

        foreach (var ev in _clip.Events)
        {
            bool fire = wrapped
                ? (ev.FrameIndex > prevIdx || ev.FrameIndex <= curIdx)
                : (ev.FrameIndex > prevIdx && ev.FrameIndex <= curIdx);

            if (fire) EventFired?.Invoke(ev);
        }
    }
}
