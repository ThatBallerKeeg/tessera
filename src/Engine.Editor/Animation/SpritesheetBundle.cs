using Avalonia.Media.Imaging;
using Engine.Core.Animation;
using Engine.Core.Sprites;

namespace Engine.Editor.Animation;

/// <summary>
/// Groups a <see cref="SpritesheetData"/> with its loaded Avalonia bitmap and the
/// <see cref="AnimationClip"/>s that reference its sprites, forming one importable
/// spritesheet asset in the editor's Assets panel.
/// </summary>
internal sealed class SpritesheetBundle
{
    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>Spritesheet frame metadata (source rects, pivots, ids).</summary>
    public SpritesheetData Sheet { get; }

    /// <summary>
    /// Avalonia bitmap loaded from <see cref="SpritesheetData.ImagePath"/>
    /// (or a programmatically generated stand-in for test fixtures).
    /// </summary>
    public Bitmap Bitmap { get; }

    /// <summary>Animation clips whose frames reference sprites in <see cref="Sheet"/>.</summary>
    public IReadOnlyList<AnimationClip> Clips { get; }

    /// <summary>Forwarded from <see cref="Sheet"/> for the Assets panel DataTemplate.</summary>
    public string Name => Sheet.Name;

    // ── Construction ──────────────────────────────────────────────────────────

    public SpritesheetBundle(
        SpritesheetData             sheet,
        Bitmap                      bitmap,
        IReadOnlyList<AnimationClip> clips)
    {
        Sheet  = sheet;
        Bitmap = bitmap;
        Clips  = clips;
    }
}
