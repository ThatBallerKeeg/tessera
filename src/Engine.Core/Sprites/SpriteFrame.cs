using Engine.Core.Math;

namespace Engine.Core.Sprites;

/// <summary>
/// One frame in a <see cref="SpritesheetData"/>: a named rectangle cropped from the PNG,
/// with an optional pivot point (render origin) expressed in source pixels.
/// </summary>
public sealed class SpriteFrame
{
    /// <summary>Stable identifier — unique within a <see cref="SpritesheetData"/>.</summary>
    public SpriteId Id { get; set; }

    /// <summary>
    /// Optional human-readable label, e.g. the Aseprite frame name like
    /// <c>"walk_south_0"</c>. Empty string when not meaningful (e.g. generated grids).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The sub-rectangle of the PNG that contains this frame's pixels.
    /// Coordinates are in source-image pixel space (top-left origin).
    /// </summary>
    public Rectangle SourceRect { get; set; }

    /// <summary>
    /// Render origin within the frame in source pixels.
    /// <c>(0, 0)</c> = top-left (default); <c>(8, 8)</c> = center of a 16×16 frame.
    /// The runtime subtracts Pivot from Position when drawing so the sprite
    /// is anchored at the GameObject's world point.
    /// </summary>
    public Vector2 Pivot { get; set; } = Vector2.Zero;
}
