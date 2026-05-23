using Engine.Core.Math;

namespace Engine.Core.Sprites;

/// <summary>
/// Asset describing a sprite sheet: the path to the source PNG and an explicit list of
/// <see cref="SpriteFrame"/>s within it.
/// <para>
/// Frames are stored explicitly rather than as a grid because Aseprite-packed sheets have
/// no guaranteed alignment.  Use <see cref="GenerateGrid"/> to produce a regular grid from
/// ad-hoc (non-Aseprite) imports.
/// </para>
/// </summary>
public sealed class SpritesheetData
{
    /// <summary>Display name of this spritesheet asset (e.g. <c>"player"</c>).</summary>
    public string Name { get; set; } = "";

    /// <summary>Absolute or project-relative path to the source PNG.</summary>
    public string ImagePath { get; set; } = "";

    /// <summary>
    /// All frames defined for this sheet. Ordering here is authoritative;
    /// <see cref="AnimationClip"/> references frames by <see cref="SpriteId"/>.
    /// </summary>
    public List<SpriteFrame> Frames { get; set; } = new();

    // ── Convenience factory ───────────────────────────────────────────────────

    /// <summary>
    /// Slices <paramref name="imageSize"/> into a uniform grid of <paramref name="frameSize"/>
    /// cells and creates one <see cref="SpriteFrame"/> per cell in row-major order (left→right,
    /// top→bottom).  SpriteIds are assigned starting at 1 to reserve 0 for
    /// <see cref="SpriteId.Empty"/>.  Pivot defaults to <c>(0,0)</c> (top-left).
    /// </summary>
    /// <param name="name">Display name for the resulting <see cref="SpritesheetData"/>.</param>
    /// <param name="imagePath">Path to the source PNG.</param>
    /// <param name="imageSize">Pixel dimensions of the full image.</param>
    /// <param name="frameSize">Pixel dimensions of one frame cell.</param>
    /// <returns>A populated <see cref="SpritesheetData"/> ready to save or use directly.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="frameSize"/> does not divide evenly into
    /// <paramref name="imageSize"/>, or when either dimension is non-positive.
    /// </exception>
    public static SpritesheetData GenerateGrid(
        string     name,
        string     imagePath,
        Vector2Int imageSize,
        Vector2Int frameSize)
    {
        if (frameSize.X <= 0 || frameSize.Y <= 0)
            throw new ArgumentException("Frame size must be positive.", nameof(frameSize));
        if (imageSize.X <= 0 || imageSize.Y <= 0)
            throw new ArgumentException("Image size must be positive.", nameof(imageSize));
        if (imageSize.X % frameSize.X != 0 || imageSize.Y % frameSize.Y != 0)
            throw new ArgumentException(
                $"Frame size {frameSize} does not divide evenly into image size {imageSize}.",
                nameof(frameSize));

        var data = new SpritesheetData { Name = name, ImagePath = imagePath };

        int cols = imageSize.X / frameSize.X;
        int rows = imageSize.Y / frameSize.Y;
        int id   = 1;   // 0 is reserved for SpriteId.Empty

        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            data.Frames.Add(new SpriteFrame
            {
                Id         = new SpriteId(id++),
                Name       = "",   // not meaningful for generated grids
                SourceRect = new Rectangle(
                    col * frameSize.X,
                    row * frameSize.Y,
                    frameSize.X,
                    frameSize.Y),
                Pivot = Vector2.Zero,
            });
        }

        return data;
    }
}
