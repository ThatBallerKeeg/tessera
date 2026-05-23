using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Engine.Core.Animation;
using Engine.Core.Sprites;

namespace Engine.Editor.Animation;

/// <summary>
/// Animation tool panel: clip browser (top) + frame list with thumbnails (bottom).
/// Populated by <see cref="SetContent"/> when a <see cref="SpritesheetBundle"/> is
/// activated in the Assets panel.
/// </summary>
public partial class AnimationToolPanel : UserControl
{
    // ── Private view-model type ───────────────────────────────────────────────

    /// <summary>
    /// One row in the frame list.  Properties are bound by the AXAML DataTemplate.
    /// </summary>
    private sealed class FrameRow
    {
        /// <summary>Cropped pixel-exact thumbnail, null when source rect is degenerate.</summary>
        public RenderTargetBitmap? Thumbnail    { get; init; }

        /// <summary>e.g. "#3"</summary>
        public string              SpriteIdText { get; init; } = "";

        /// <summary>e.g. "120 ms"</summary>
        public string              DurationText { get; init; } = "";
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private SpritesheetData?             _sheet;
    private Bitmap?                      _sheetBitmap;

    private readonly ObservableCollection<AnimationClip> _clipItems  = new();
    private readonly ObservableCollection<FrameRow>      _frameItems = new();

    // Tracks all RenderTargetBitmaps created for the current frame list so they
    // can be disposed when the selection changes or SetContent is called again.
    private readonly List<RenderTargetBitmap> _thumbCache = new();

    // ── Construction ──────────────────────────────────────────────────────────

    public AnimationToolPanel()
    {
        InitializeComponent();
        ClipBrowser.ItemsSource = _clipItems;
        FrameList.ItemsSource   = _frameItems;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the clip browser with <paramref name="clips"/>.
    /// Clears the frame list until the user clicks a clip.
    /// Passing <see langword="null"/> for any argument resets the panel to empty.
    /// </summary>
    public void SetContent(
        SpritesheetData?             sheet,
        Bitmap?                      bitmap,
        IReadOnlyList<AnimationClip>? clips)
    {
        _sheet       = sheet;
        _sheetBitmap = bitmap;

        DisposeAndClearThumbnails();
        _clipItems.Clear();
        _frameItems.Clear();

        if (clips is not null)
            foreach (var c in clips)
                _clipItems.Add(c);
    }

    // ── Clip selection ────────────────────────────────────────────────────────

    private void OnClipSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        DisposeAndClearThumbnails();
        _frameItems.Clear();

        if (ClipBrowser.SelectedItem is not AnimationClip clip) return;
        if (_sheet is null) return;

        foreach (var clipFrame in clip.Frames)
        {
            // Find the SpriteFrame in the sheet to get the source rectangle.
            SpriteFrame? sf = null;
            foreach (var f in _sheet.Frames)
            {
                if (f.Id == clipFrame.SpriteId) { sf = f; break; }
            }

            RenderTargetBitmap? thumb = null;
            if (sf is not null && _sheetBitmap is not null)
            {
                thumb = CropFrame(_sheetBitmap, sf);
                if (thumb is not null)
                    _thumbCache.Add(thumb);
            }

            _frameItems.Add(new FrameRow
            {
                Thumbnail    = thumb,
                SpriteIdText = $"#{clipFrame.SpriteId.Value}",
                DurationText = $"{clipFrame.DurationMs} ms",
            });
        }
    }

    // ── Thumbnail helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Crops <paramref name="frame"/>'s source rectangle from <paramref name="sheet"/>
    /// into a fresh <see cref="RenderTargetBitmap"/>.
    /// Uses the same <c>DrawImage(source, srcRect, destRect)</c> pattern as the
    /// ghost-tile preview in the viewport.
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
    }
}
