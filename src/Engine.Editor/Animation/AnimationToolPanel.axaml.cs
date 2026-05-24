using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Engine.Core.Animation;
using Engine.Core.Sprites;
using Engine.Runtime.Components;

namespace Engine.Editor.Animation;

/// <summary>
/// Animation tool panel: clip browser (top) + frame list with thumbnails (middle) +
/// live preview + timeline scrubber + transport controls (bottom).
/// <para>
/// Playback reuses the runtime <see cref="Animator"/> component directly — same
/// frame-advancement and event-crossing logic as in-game, no reimplementation.
/// The Animator is ticked from a <see cref="DispatcherTimer"/> at ~60 fps.
/// </para>
/// Populated by <see cref="SetContent"/> when a <see cref="SpritesheetBundle"/> is
/// activated in the Assets panel.
/// </summary>
public partial class AnimationToolPanel : UserControl
{
    // ── Private view-model type ───────────────────────────────────────────────

    /// <summary>One row in the frame list.  Properties are bound by the AXAML DataTemplate.</summary>
    private sealed class FrameRow
    {
        /// <summary>Cropped pixel-exact thumbnail, null when source rect is degenerate.</summary>
        public RenderTargetBitmap? Thumbnail    { get; init; }
        /// <summary>e.g. "#3"</summary>
        public string              SpriteIdText { get; init; } = "";
        /// <summary>e.g. "120 ms"</summary>
        public string              DurationText { get; init; } = "";
    }

    // ── Asset state ───────────────────────────────────────────────────────────

    private SpritesheetData? _sheet;
    private Bitmap?          _sheetBitmap;
    private AnimationClip?   _activeClip;

    private readonly ObservableCollection<AnimationClip> _clipItems       = new();
    private readonly ObservableCollection<FrameRow>      _frameItems      = new();
    private readonly List<RenderTargetBitmap>            _thumbCache      = new();

    // Maps SpriteId → index into _thumbCache (first occurrence per sprite wins).
    // Used to update the preview bitmap during playback without new allocations.
    private readonly Dictionary<SpriteId, int>           _frameThumbIndex = new();

    // ── Playback state ────────────────────────────────────────────────────────

    // Standalone Animator — no GameObject required; OnUpdate/SeekMs/StepXxx work
    // without an Owner being set.
    private readonly Animator _animator = new();
    private DispatcherTimer?  _timer;
    private bool              _isPlaying;
    private DateTime          _lastTick;

    // ── Timeline state ────────────────────────────────────────────────────────

    // The playhead is the only canvas child whose position changes on every tick.
    // All other children (track bar, frame marks, event flags) are static within
    // a given clip; they're replaced wholesale by RebuildTimeline().
    private Rectangle? _playheadRect;
    private bool        _isDraggingPlayhead;

    // Cached so UpdatePreview can skip a redraw when the frame hasn't changed.
    private SpriteId    _lastPreviewSpriteId = SpriteId.Empty;

    // ── Construction ──────────────────────────────────────────────────────────

    public AnimationToolPanel()
    {
        InitializeComponent();
        ClipBrowser.ItemsSource    = _clipItems;
        FrameList.ItemsSource      = _frameItems;
        // SizeChanged wired in code (same pattern as MonoGameViewport) to reposition
        // all timeline markers when the panel is resized.
        TimelineCanvas.SizeChanged += OnTimelineSizeChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the clip browser with <paramref name="clips"/>.
    /// Clears the frame list, preview, and timeline until the user clicks a clip.
    /// Passing <see langword="null"/> for any argument resets the panel to empty.
    /// </summary>
    public void SetContent(
        SpritesheetData?              sheet,
        Bitmap?                       bitmap,
        IReadOnlyList<AnimationClip>? clips)
    {
        StopPlayback();
        _sheet       = sheet;
        _sheetBitmap = bitmap;
        _activeClip  = null;

        DisposeAndClearThumbnails();
        _clipItems.Clear();
        _frameItems.Clear();

        _lastPreviewSpriteId = SpriteId.Empty;
        PreviewImage.Source  = null;
        ClearTimeline();

        if (clips is not null)
            foreach (var c in clips)
                _clipItems.Add(c);
    }

    // ── Clip selection ────────────────────────────────────────────────────────

    private void OnClipSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        StopPlayback();
        DisposeAndClearThumbnails();
        _frameItems.Clear();
        _lastPreviewSpriteId = SpriteId.Empty;
        ClearTimeline();

        if (ClipBrowser.SelectedItem is not AnimationClip clip)
        {
            _activeClip         = null;
            PreviewImage.Source = null;
            return;
        }

        _activeClip = clip;

        // ── Build frame list + thumbnail index ──
        if (_sheet is not null)
        {
            for (int i = 0; i < clip.Frames.Count; i++)
            {
                var clipFrame = clip.Frames[i];

                SpriteFrame? sf = null;
                foreach (var f in _sheet.Frames)
                    if (f.Id == clipFrame.SpriteId) { sf = f; break; }

                RenderTargetBitmap? thumb = null;
                if (sf is not null && _sheetBitmap is not null)
                {
                    thumb = CropFrame(_sheetBitmap, sf);
                    if (thumb is not null)
                    {
                        int idx = _thumbCache.Count;
                        _thumbCache.Add(thumb);
                        _frameThumbIndex.TryAdd(clipFrame.SpriteId, idx);
                    }
                }

                _frameItems.Add(new FrameRow
                {
                    Thumbnail    = thumb,
                    SpriteIdText = $"#{clipFrame.SpriteId.Value}",
                    DurationText = $"{clipFrame.DurationMs} ms",
                });
            }
        }

        // Prime the animator on frame 0 — no autoplay, just seed CurrentSpriteId.
        _animator.Play(clip);

        RebuildTimeline();
        UpdatePreview(_animator.CurrentSpriteId);
    }

    // ── Transport controls ────────────────────────────────────────────────────

    private void OnPlay(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null) return;
        // Restart non-looping clips that have already finished.
        if (_animator.IsComplete)
            _animator.Play(_activeClip);
        if (!_isPlaying)
            StartPlayback();
    }

    private void OnPause(object? sender, RoutedEventArgs e)  => PausePlayback();
    private void OnStop(object? sender, RoutedEventArgs e)   => StopPlayback();

    private void OnStepForward(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null) return;
        PausePlayback();
        _animator.StepForward();
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();
    }

    private void OnStepBack(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null) return;
        PausePlayback();
        _animator.StepBack();
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();
    }

    // ── Playback helpers ──────────────────────────────────────────────────────

    private void StartPlayback()
    {
        if (_timer is null)
        {
            _timer       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTimerTick;
        }
        _isPlaying = true;
        _lastTick  = DateTime.UtcNow;
        _timer.Start();
    }

    private void PausePlayback()
    {
        _isPlaying = false;
        _timer?.Stop();
    }

    /// <summary>
    /// Stops playback and resets the animator to frame 0.
    /// Called by Stop button, clip selection, and SetContent.
    /// </summary>
    private void StopPlayback()
    {
        _isPlaying = false;
        _timer?.Stop();
        if (_activeClip is not null)
        {
            _animator.Play(_activeClip);   // resets elapsed + prevFrameIndex + CurrentSpriteId
            UpdatePreview(_animator.CurrentSpriteId);
            UpdatePlayheadPosition();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _activeClip is null) return;

        var   now   = DateTime.UtcNow;
        float delta = (float)(now - _lastTick).TotalSeconds;
        _lastTick   = now;
        // Cap at 100 ms so a window freeze doesn't cause a large jump.
        delta = MathF.Min(delta, 0.1f);

        _animator.OnUpdate(delta);
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();

        // Auto-pause once a non-looping clip reaches its end.
        if (_animator.IsComplete)
            PausePlayback();
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets <see cref="PreviewImage"/> to the cached thumbnail for <paramref name="spriteId"/>.
    /// No-op when the displayed sprite hasn't changed (avoids unnecessary UI invalidation
    /// at 60 fps during playback).
    /// </summary>
    private void UpdatePreview(SpriteId spriteId)
    {
        if (spriteId == _lastPreviewSpriteId) return;
        _lastPreviewSpriteId = spriteId;

        if (spriteId != SpriteId.Empty &&
            _frameThumbIndex.TryGetValue(spriteId, out int idx) &&
            idx < _thumbCache.Count)
        {
            PreviewImage.Source = _thumbCache[idx];
        }
        else
        {
            PreviewImage.Source = null;
        }
    }

    // ── Timeline ─────────────────────────────────────────────────────────────

    private void OnTimelineSizeChanged(object? sender, SizeChangedEventArgs e)
        => RebuildTimeline();

    /// <summary>
    /// Clears and re-populates the timeline canvas:
    /// <list type="bullet">
    ///   <item>Horizontal track bar</item>
    ///   <item>Thin vertical marks at each frame boundary</item>
    ///   <item>Yellow flag rectangles at animation-event frame positions</item>
    ///   <item>Cyan playhead line (added last so it renders on top)</item>
    /// </list>
    /// Called when a clip is selected and whenever the canvas is resized.
    /// </summary>
    private void RebuildTimeline()
    {
        ClearTimeline();
        if (_activeClip is null || _animator.TotalDurationMs <= 0f) return;

        double w = TimelineCanvas.Bounds.Width;
        double h = TimelineCanvas.Bounds.Height;
        if (w <= 0) return;   // canvas not yet laid out — SizeChanged will fire later

        // ── Track bar ──
        var track = new Rectangle
        {
            Width  = w,
            Height = 2,
            Fill   = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
        };
        Canvas.SetLeft(track, 0);
        Canvas.SetTop(track,  System.Math.Floor(h * 0.5) - 1);
        TimelineCanvas.Children.Add(track);

        // ── Frame boundary marks (skip frame 0 — starts at the left edge) ──
        float cum = 0f;
        for (int i = 0; i < _activeClip.Frames.Count; i++)
        {
            if (i > 0)
            {
                double x    = cum / _animator.TotalDurationMs * w;
                var    mark = new Rectangle
                {
                    Width  = 1,
                    Height = h,
                    Fill   = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                };
                Canvas.SetLeft(mark, x);
                Canvas.SetTop(mark, 0);
                TimelineCanvas.Children.Add(mark);
            }
            cum += _activeClip.Frames[i].DurationMs;
        }

        // ── Event marker flags ──
        // Build per-frame start-time array to map event.FrameIndex → X position.
        var frameStartMs = new float[_activeClip.Frames.Count];
        float t = 0f;
        for (int i = 0; i < _activeClip.Frames.Count; i++)
        {
            frameStartMs[i]  = t;
            t               += _activeClip.Frames[i].DurationMs;
        }

        foreach (var ev in _activeClip.Events)
        {
            if (ev.FrameIndex < 0 || ev.FrameIndex >= frameStartMs.Length) continue;
            double x    = frameStartMs[ev.FrameIndex] / _animator.TotalDurationMs * w;
            var    flag = new Rectangle
            {
                Width  = 3,
                Height = 9,
                Fill   = new SolidColorBrush(Colors.Yellow),
            };
            ToolTip.SetTip(flag, string.IsNullOrEmpty(ev.Name) ? "event" : ev.Name);
            Canvas.SetLeft(flag, x);
            Canvas.SetTop(flag, 1);
            TimelineCanvas.Children.Add(flag);
        }

        // ── Playhead — added last so it always renders above other marks ──
        _playheadRect = new Rectangle
        {
            Width  = 2,
            Height = h,
            Fill   = new SolidColorBrush(Colors.Cyan),
        };
        TimelineCanvas.Children.Add(_playheadRect);
        UpdatePlayheadPosition();
    }

    /// <summary>
    /// Moves the playhead line to the canvas X position corresponding to
    /// <see cref="Animator.ElapsedMs"/> / <see cref="Animator.TotalDurationMs"/>.
    /// </summary>
    private void UpdatePlayheadPosition()
    {
        if (_playheadRect is null) return;
        double w = TimelineCanvas.Bounds.Width;
        if (w <= 0 || _animator.TotalDurationMs <= 0f) return;

        double ratio = System.Math.Clamp(
            (double)_animator.ElapsedMs / _animator.TotalDurationMs, 0.0, 1.0);
        // Subtract 1 to centre the 2-px-wide rect on the logical position.
        Canvas.SetLeft(_playheadRect, ratio * w - 1.0);
    }

    private void ClearTimeline()
    {
        TimelineCanvas.Children.Clear();
        _playheadRect = null;
    }

    // ── Timeline pointer events ───────────────────────────────────────────────

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeClip is null) return;
        _isDraggingPlayhead = true;
        SeekFromPointer(e.GetPosition(TimelineCanvas).X);
        e.Handled = true;
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingPlayhead) return;
        // Release detection: if the left button is no longer held, stop dragging
        // (handles the case where the pointer was released outside the canvas).
        if (!e.GetCurrentPoint(TimelineCanvas).Properties.IsLeftButtonPressed)
        {
            _isDraggingPlayhead = false;
            return;
        }
        SeekFromPointer(e.GetPosition(TimelineCanvas).X);
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
        => _isDraggingPlayhead = false;

    private void SeekFromPointer(double x)
    {
        double w = TimelineCanvas.Bounds.Width;
        if (w <= 0 || _animator.TotalDurationMs <= 0f) return;

        float ms = (float)(System.Math.Clamp(x / w, 0.0, 1.0) * _animator.TotalDurationMs);
        _animator.SeekMs(ms);

        // Force a preview refresh even if the SpriteId is unchanged (e.g. scrubbing
        // within the same frame after returning from a different frame).
        _lastPreviewSpriteId = SpriteId.Empty;
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();
    }

    // ── Thumbnail helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Crops <paramref name="frame"/>'s source rectangle from <paramref name="sheet"/>
    /// into a fresh <see cref="RenderTargetBitmap"/>.
    /// Same <c>DrawImage(source, srcRect, destRect)</c> pattern as the ghost-tile preview.
    /// Returns <see langword="null"/> when the source rect is degenerate.
    /// </summary>
    private static RenderTargetBitmap? CropFrame(Bitmap sheet, SpriteFrame frame)
    {
        int w = (int)frame.SourceRect.Width;
        int h = (int)frame.SourceRect.Height;
        if (w <= 0 || h <= 0) return null;

        try
        {
            var bm = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            using var ctx = bm.CreateDrawingContext();
            ctx.DrawImage(
                sheet,
                new Rect(frame.SourceRect.X, frame.SourceRect.Y, w, h),
                new Rect(0, 0, w, h));
            return bm;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AnimationPanel] Thumbnail crop failed: {ex.Message}");
            return null;
        }
    }

    private void DisposeAndClearThumbnails()
    {
        foreach (var t in _thumbCache)
            t.Dispose();
        _thumbCache.Clear();
        _frameThumbIndex.Clear();
    }
}
