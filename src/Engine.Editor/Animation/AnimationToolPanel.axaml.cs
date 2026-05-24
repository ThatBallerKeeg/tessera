using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Engine.Core.Animation;
using Engine.Core.Sprites;
using Engine.Runtime.Components;

namespace Engine.Editor.Animation;

/// <summary>
/// Animation tool panel: clip browser → editable frame list with drag-reorder
/// → live preview → timeline scrubber with event markers → transport controls.
/// </summary>
public partial class AnimationToolPanel : UserControl
{
    // ── Mutable frame row (view-model) ────────────────────────────────────────

    /// <summary>
    /// View-model for one row in the frame list.
    /// <see cref="DurationMs"/> is two-way bound to the NumericUpDown; its setter
    /// writes through to the underlying <see cref="AnimationClipFrame"/> and
    /// triggers a timeline/animator resync via <paramref name="onDurationChanged"/>.
    /// </summary>
    private sealed class FrameRow : INotifyPropertyChanged
    {
        private readonly AnimationClipFrame _clipFrame;
        private readonly Action             _onDurationChanged;

        public RenderTargetBitmap? Thumbnail    { get; }
        public string              SpriteIdText { get; }
        /// <summary>False when this is the only frame in the clip — disables the × button.</summary>
        public bool                CanRemove    { get; }

        /// <summary>
        /// Frame duration in milliseconds.  Two-way bound to NumericUpDown.
        /// Setter clamps to [1, 10000] and calls back to rebuild the timeline.
        /// </summary>
        public decimal DurationMs
        {
            get => _clipFrame.DurationMs;
            set
            {
                int clamped = System.Math.Clamp((int)value, 1, 10_000);
                if (_clipFrame.DurationMs == clamped) return;
                _clipFrame.DurationMs = clamped;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationMs)));
                _onDurationChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FrameRow(
            RenderTargetBitmap? thumbnail,
            string              spriteIdText,
            AnimationClipFrame  clipFrame,
            Action              onDurationChanged,
            bool                canRemove)
        {
            Thumbnail          = thumbnail;
            SpriteIdText       = spriteIdText;
            _clipFrame         = clipFrame;
            _onDurationChanged = onDurationChanged;
            CanRemove          = canRemove;
        }
    }

    // ── Asset state ───────────────────────────────────────────────────────────

    private SpritesheetData? _sheet;
    private Bitmap?          _sheetBitmap;
    private AnimationClip?   _activeClip;

    private readonly ObservableCollection<AnimationClip> _clipItems       = new();
    private readonly ObservableCollection<FrameRow>      _frameItems      = new();
    private readonly List<RenderTargetBitmap>            _thumbCache      = new();
    private readonly Dictionary<SpriteId, int>           _frameThumbIndex = new();

    // ── Playback state ────────────────────────────────────────────────────────

    private readonly Animator _animator = new();
    private DispatcherTimer?  _timer;
    private bool              _isPlaying;
    private DateTime          _lastTick;

    // ── Timeline state ────────────────────────────────────────────────────────

    private Rectangle?                                  _playheadRect;
    private readonly Dictionary<Rectangle, AnimationEvent> _eventFlagMap = new();
    private bool                                        _isDraggingPlayhead;
    private SpriteId                                    _lastPreviewSpriteId = SpriteId.Empty;

    // ── Drag-reorder state ────────────────────────────────────────────────────

    private FrameRow? _dragRow;
    private int       _dragSrcIdx;
    private double    _dragStartY;
    private bool      _isDraggingFrame;

    // ── Construction ──────────────────────────────────────────────────────────

    public AnimationToolPanel()
    {
        InitializeComponent();
        ClipBrowser.ItemsSource    = _clipItems;
        FrameList.ItemsSource      = _frameItems;
        TimelineCanvas.SizeChanged += OnTimelineSizeChanged;

        // Drag-reorder: track pointer move and release at the panel level so the
        // gesture continues even when the pointer leaves the source row.
        this.AddHandler(PointerMovedEvent,
                        OnFrameDragPointerMoved,
                        RoutingStrategies.Bubble,
                        handledEventsToo: true);
        this.AddHandler(PointerReleasedEvent,
                        OnFrameDragPointerReleased,
                        RoutingStrategies.Bubble,
                        handledEventsToo: true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the panel from a <see cref="SpritesheetBundle"/>.
    /// Pass <see langword="null"/> for all arguments to reset to empty.
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
        RebuildFrameItemsFromClip();
        _animator.Play(clip);
        RebuildTimeline();
        UpdatePreview(_animator.CurrentSpriteId);
    }

    // ── Transport controls ────────────────────────────────────────────────────

    private void OnPlay(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null) return;
        if (_animator.IsComplete) _animator.Play(_activeClip);
        if (!_isPlaying) StartPlayback();
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

    private void StopPlayback()
    {
        _isPlaying = false;
        _timer?.Stop();
        if (_activeClip is not null)
        {
            _animator.Play(_activeClip);
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
        delta       = MathF.Min(delta, 0.1f);
        _animator.OnUpdate(delta);
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();
        if (_animator.IsComplete) PausePlayback();
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void UpdatePreview(SpriteId spriteId)
    {
        if (spriteId == _lastPreviewSpriteId) return;
        _lastPreviewSpriteId = spriteId;
        if (spriteId != SpriteId.Empty &&
            _frameThumbIndex.TryGetValue(spriteId, out int idx) &&
            idx < _thumbCache.Count)
            PreviewImage.Source = _thumbCache[idx];
        else
            PreviewImage.Source = null;
    }

    // ── Frame list management ─────────────────────────────────────────────────

    /// <summary>
    /// Disposes all cached thumbnails, clears the frame-item collection, then
    /// rebuilds both from <see cref="_activeClip"/> and <see cref="_sheet"/>.
    /// Called on clip selection, add-frame, and remove-frame.
    /// </summary>
    private void RebuildFrameItemsFromClip()
    {
        DisposeAndClearThumbnails();
        _frameItems.Clear();

        if (_activeClip is null || _sheet is null) return;
        bool moreThanOne = _activeClip.Frames.Count > 1;

        foreach (var cf in _activeClip.Frames)
        {
            SpriteFrame? sf = null;
            foreach (var f in _sheet.Frames)
                if (f.Id == cf.SpriteId) { sf = f; break; }

            RenderTargetBitmap? thumb = null;
            if (sf is not null && _sheetBitmap is not null)
            {
                thumb = CropFrame(_sheetBitmap, sf);
                if (thumb is not null)
                {
                    int tIdx = _thumbCache.Count;
                    _thumbCache.Add(thumb);
                    _frameThumbIndex.TryAdd(cf.SpriteId, tIdx);
                }
            }

            _frameItems.Add(new FrameRow(
                thumb,
                $"#{cf.SpriteId.Value}",
                cf,
                AfterFrameListChanged,
                canRemove: moreThanOne));
        }
    }

    /// <summary>
    /// Called after any structural change to the clip's frame list or a duration edit.
    /// Resyncs the Animator (so in-flight playback uses the new durations/order),
    /// rebuilds the timeline, and refreshes the preview.
    /// </summary>
    private void AfterFrameListChanged()
    {
        if (_activeClip is null) return;
        _animator.ResyncClip();
        RebuildTimeline();
        _lastPreviewSpriteId = SpriteId.Empty;
        UpdatePreview(_animator.CurrentSpriteId);
    }

    // ── Frame editing — add / remove ──────────────────────────────────────────

    private async void OnAddFrame(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null || _sheet is null || _sheetBitmap is null) return;

        SpriteId? picked = await PickSprite();
        if (picked is null) return;

        _activeClip.Frames.Add(new AnimationClipFrame { SpriteId = picked.Value, DurationMs = 100 });
        RebuildFrameItemsFromClip();
        AfterFrameListChanged();
    }

    private void OnFrameRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (_activeClip is null) return;
        if (sender is not Button { Tag: FrameRow row }) return;
        if (_activeClip.Frames.Count <= 1) return;  // guard: keep at least one frame

        int idx = _frameItems.IndexOf(row);
        if (idx < 0 || idx >= _activeClip.Frames.Count) return;

        _activeClip.Frames.RemoveAt(idx);
        RebuildFrameItemsFromClip();
        AfterFrameListChanged();
    }

    // ── Sprite picker dialog ──────────────────────────────────────────────────

    private async Task<SpriteId?> PickSprite()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null || _sheet is null || _sheetBitmap is null) return null;

        SpriteId?  result = null;
        var        thumbsToDispose = new List<RenderTargetBitmap>();
        var        wrap   = new WrapPanel { Margin = new Thickness(4) };

        var dialog = new Window
        {
            Title                 = "Pick Sprite",
            Width                 = 240,
            Height                = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize             = true,
            ShowInTaskbar         = false,
            Content               = new ScrollViewer { Content = wrap },
        };

        foreach (var frame in _sheet.Frames)
        {
            var thumb = CropFrame(_sheetBitmap, frame);
            if (thumb is not null) thumbsToDispose.Add(thumb);

            var capturedId = frame.Id;
            var btn = new Button
            {
                Width   = 52,
                Height  = 52,
                Padding = new Thickness(2),
                Margin  = new Thickness(2),
                Content = new Image
                {
                    Source  = thumb,
                    Stretch = Stretch.Uniform,
                },
            };
            RenderOptions.SetBitmapInterpolationMode(btn, BitmapInterpolationMode.None);
            ToolTip.SetTip(btn, $"#{capturedId.Value}");
            btn.Click += (_, _) => { result = capturedId; dialog.Close(); };
            wrap.Children.Add(btn);
        }

        await dialog.ShowDialog(owner);

        foreach (var t in thumbsToDispose) t.Dispose();
        return result;
    }

    // ── Drag-reorder ──────────────────────────────────────────────────────────

    private void OnFrameRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DockPanel { Tag: FrameRow row }) return;
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        // Let buttons and text inputs handle their own press events.
        if (e.Source is Button or TextBox) return;

        int idx = _frameItems.IndexOf(row);
        if (idx < 0) return;

        _dragRow         = row;
        _dragSrcIdx      = idx;
        _dragStartY      = e.GetPosition(FrameList).Y;
        _isDraggingFrame = false;
    }

    private void OnFrameDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragRow is null) return;
        if (System.Math.Abs(e.GetPosition(FrameList).Y - _dragStartY) > 8)
            _isDraggingFrame = true;
    }

    private void OnFrameDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingFrame || _dragRow is null || _activeClip is null)
        {
            _dragRow = null; _isDraggingFrame = false;
            return;
        }

        int tgt = GetFrameIndexAtY(e.GetPosition(FrameList).Y);
        if (tgt != _dragSrcIdx && tgt >= 0 && tgt < _frameItems.Count)
        {
            // Reorder model and view in sync.
            var frame = _activeClip.Frames[_dragSrcIdx];
            _activeClip.Frames.RemoveAt(_dragSrcIdx);
            _activeClip.Frames.Insert(tgt, frame);
            _frameItems.Move(_dragSrcIdx, tgt);
            AfterFrameListChanged();
        }

        _dragRow = null; _isDraggingFrame = false;
    }

    /// <summary>Returns the frame-list row index at the given Y pixel (ListBox coords).</summary>
    private int GetFrameIndexAtY(double y)
    {
        const double rowH = 46.0; // Height=44 + Margin top+bottom ≈ 46
        int idx = (int)(y / rowH);
        return System.Math.Clamp(idx, 0, _frameItems.Count - 1);
    }

    // ── Timeline ─────────────────────────────────────────────────────────────

    private void OnTimelineSizeChanged(object? sender, SizeChangedEventArgs e)
        => RebuildTimeline();

    private void RebuildTimeline()
    {
        ClearTimeline();
        if (_activeClip is null || _animator.TotalDurationMs <= 0f) return;

        double w = TimelineCanvas.Bounds.Width;
        double h = TimelineCanvas.Bounds.Height;
        if (w <= 0) return;

        // Track bar
        var track = new Rectangle
        {
            Width  = w, Height = 2,
            Fill   = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
        };
        Canvas.SetLeft(track, 0);
        Canvas.SetTop(track, System.Math.Floor(h * 0.5) - 1);
        TimelineCanvas.Children.Add(track);

        // Frame boundary marks (skip index 0 — starts at left edge)
        float cum = 0f;
        for (int i = 0; i < _activeClip.Frames.Count; i++)
        {
            if (i > 0)
            {
                double x = cum / _animator.TotalDurationMs * w;
                var mark = new Rectangle
                {
                    Width = 1, Height = h,
                    Fill  = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                };
                Canvas.SetLeft(mark, x); Canvas.SetTop(mark, 0);
                TimelineCanvas.Children.Add(mark);
            }
            cum += _activeClip.Frames[i].DurationMs;
        }

        // Event marker flags — build per-frame start times first
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
            double x = frameStartMs[ev.FrameIndex] / _animator.TotalDurationMs * w;
            var flag = new Rectangle
            {
                Width = 3, Height = 9,
                Fill  = new SolidColorBrush(Colors.Yellow),
            };
            ToolTip.SetTip(flag, string.IsNullOrEmpty(ev.Name) ? "event" : ev.Name);
            Canvas.SetLeft(flag, x); Canvas.SetTop(flag, 1);
            TimelineCanvas.Children.Add(flag);
            _eventFlagMap[flag] = ev;          // register for right-click hit-test
        }

        // Playhead — rendered last so it sits on top
        _playheadRect = new Rectangle
        {
            Width = 2, Height = h,
            Fill  = new SolidColorBrush(Colors.Cyan),
        };
        TimelineCanvas.Children.Add(_playheadRect);
        UpdatePlayheadPosition();
    }

    private void UpdatePlayheadPosition()
    {
        if (_playheadRect is null) return;
        double w = TimelineCanvas.Bounds.Width;
        if (w <= 0 || _animator.TotalDurationMs <= 0f) return;
        double ratio = System.Math.Clamp(
            (double)_animator.ElapsedMs / _animator.TotalDurationMs, 0.0, 1.0);
        Canvas.SetLeft(_playheadRect, ratio * w - 1.0);
    }

    private void ClearTimeline()
    {
        TimelineCanvas.Children.Clear();
        _eventFlagMap.Clear();
        _playheadRect = null;
    }

    // ── Timeline pointer (scrub + right-click context menu) ───────────────────

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeClip is null) return;
        var point = e.GetCurrentPoint(TimelineCanvas);

        if (point.Properties.IsRightButtonPressed)
        {
            // Identify whether the pointer landed on an event flag.
            AnimationEvent? hit = null;
            if (e.Source is Rectangle r) _eventFlagMap.TryGetValue(r, out hit);
            ShowTimelineContextMenu(hit, point.Position.X);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            _isDraggingPlayhead = true;
            SeekFromPointer(point.Position.X);
            e.Handled = true;
        }
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingPlayhead) return;
        if (!e.GetCurrentPoint(TimelineCanvas).Properties.IsLeftButtonPressed)
        { _isDraggingPlayhead = false; return; }
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
        _lastPreviewSpriteId = SpriteId.Empty;
        UpdatePreview(_animator.CurrentSpriteId);
        UpdatePlayheadPosition();
    }

    // ── Timeline context menu ─────────────────────────────────────────────────

    private void ShowTimelineContextMenu(AnimationEvent? hitEvent, double xPos)
    {
        var menu = new ContextMenu();

        var addItem = new MenuItem { Header = "Add Event Here…" };
        addItem.Click += async (_, _) => await AddEventAtPosition(xPos);
        menu.Items.Add(addItem);

        if (hitEvent is not null)
        {
            menu.Items.Add(new Separator());
            string label = string.IsNullOrEmpty(hitEvent.Name)
                ? "Remove Event"
                : $"Remove \"{hitEvent.Name}\"";
            var removeItem = new MenuItem { Header = label };
            removeItem.Click += (_, _) => RemoveEvent(hitEvent);
            menu.Items.Add(removeItem);
        }

        menu.Open(TimelineCanvas);
    }

    private async Task AddEventAtPosition(double xPos)
    {
        if (_activeClip is null || _animator.TotalDurationMs <= 0f) return;
        double w = TimelineCanvas.Bounds.Width;
        if (w <= 0) return;

        float ms = (float)(xPos / w * _animator.TotalDurationMs);

        // Find the frame index at this time position.
        int frameIdx = 0;
        float cum = 0f;
        for (int i = 0; i < _activeClip.Frames.Count; i++)
        {
            if (ms >= cum && ms < cum + _activeClip.Frames[i].DurationMs)
            { frameIdx = i; break; }
            cum     += _activeClip.Frames[i].DurationMs;
            frameIdx = i; // fallback: last frame
        }

        string? name = await PromptForText("Add Event", "Event name:", "");
        if (name is null) return;   // cancelled

        _activeClip.Events.Add(new AnimationEvent { Name = name, FrameIndex = frameIdx });
        RebuildTimeline();
    }

    private void RemoveEvent(AnimationEvent ev)
    {
        _activeClip?.Events.Remove(ev);
        RebuildTimeline();
    }

    // ── Text-prompt dialog (built in code — no extra AXAML file needed) ───────

    private async Task<string?> PromptForText(string title, string message, string defaultText)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return null;

        string? result  = null;
        var     textBox = new TextBox { Text = defaultText, Margin = new Thickness(8, 4, 8, 8) };

        var okBtn = new Button
        {
            Content  = "OK",
            IsDefault = true,
            Width    = 64,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var cancelBtn = new Button
        {
            Content  = "Cancel",
            IsCancel  = true,
            Width    = 64,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(8, 0, 8, 8),
            Spacing             = 6,
        };
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(okBtn);

        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = message, Margin = new Thickness(8, 8, 8, 4) });
        body.Children.Add(textBox);
        body.Children.Add(btnRow);

        var dialog = new Window
        {
            Title                 = title,
            Content               = body,
            Width                 = 280,
            SizeToContent         = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize             = false,
            ShowInTaskbar         = false,
        };

        okBtn.Click     += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancelBtn.Click += (_, _) => dialog.Close();
        dialog.Opened   += (_, _) => { textBox.Focus(); textBox.SelectAll(); };

        await dialog.ShowDialog(owner);
        return result;
    }

    // ── Thumbnail helpers ─────────────────────────────────────────────────────

    private static RenderTargetBitmap? CropFrame(Bitmap sheet, SpriteFrame frame)
    {
        int w = (int)frame.SourceRect.Width;
        int h = (int)frame.SourceRect.Height;
        if (w <= 0 || h <= 0) return null;
        try
        {
            var bm = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            using var ctx = bm.CreateDrawingContext();
            ctx.DrawImage(sheet,
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
        foreach (var t in _thumbCache) t.Dispose();
        _thumbCache.Clear();
        _frameThumbIndex.Clear();
    }
}
