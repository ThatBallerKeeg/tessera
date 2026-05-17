namespace Engine.Core.Math;

/// <summary>
/// Immutable axis-aligned rectangle with <see cref="float"/> components.
/// Stored as four floats: X, Y (top-left corner), Width, Height.
/// </summary>
public readonly struct Rectangle : IEquatable<Rectangle>
{
    /// <summary>X coordinate of the left edge.</summary>
    public float X { get; }

    /// <summary>Y coordinate of the top edge.</summary>
    public float Y { get; }

    /// <summary>Width of the rectangle.</summary>
    public float Width { get; }

    /// <summary>Height of the rectangle.</summary>
    public float Height { get; }

    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    /// <summary>Top-left corner as a <see cref="Vector2"/>.</summary>
    public Vector2 Position => new(X, Y);

    /// <summary>Width and height as a <see cref="Vector2"/>.</summary>
    public Vector2 Size => new(Width, Height);

    /// <summary>Center point of the rectangle.</summary>
    public Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>Creates a rectangle from explicit components.</summary>
    public Rectangle(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    /// <summary>Creates a rectangle from a position and size.</summary>
    public Rectangle(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y) { }

    /// <summary>Rectangle at the origin with zero size.</summary>
    public static readonly Rectangle Zero = new(0f, 0f, 0f, 0f);

    /// <summary>Unit rectangle: origin, 1×1.</summary>
    public static readonly Rectangle One = new(0f, 0f, 1f, 1f);

    /// <summary>Returns this rectangle translated by <paramref name="offset"/>.</summary>
    public static Rectangle operator +(Rectangle r, Vector2 offset) =>
        new(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height);

    /// <summary>Returns this rectangle translated by the negation of <paramref name="offset"/>.</summary>
    public static Rectangle operator -(Rectangle r, Vector2 offset) =>
        new(r.X - offset.X, r.Y - offset.Y, r.Width, r.Height);

    /// <summary>Returns this rectangle with all components scaled by <paramref name="scalar"/>.</summary>
    public static Rectangle operator *(Rectangle r, float scalar) =>
        new(r.X * scalar, r.Y * scalar, r.Width * scalar, r.Height * scalar);

    public static bool operator ==(Rectangle a, Rectangle b) => a.Equals(b);
    public static bool operator !=(Rectangle a, Rectangle b) => !a.Equals(b);

    /// <summary>Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>.</summary>
    public static Rectangle Lerp(Rectangle a, Rectangle b, float t) =>
        new(a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Width + (b.Width - a.Width) * t,
            a.Height + (b.Height - a.Height) * t);

    /// <summary>Returns the Euclidean distance between the centers of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Distance(Rectangle a, Rectangle b)
    {
        float cx = (a.X + a.Width * 0.5f) - (b.X + b.Width * 0.5f);
        float cy = (a.Y + a.Height * 0.5f) - (b.Y + b.Height * 0.5f);
        return MathF.Sqrt(cx * cx + cy * cy);
    }

    /// <summary>Returns true if <paramref name="point"/> lies within or on the boundary of this rectangle.</summary>
    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>Returns true if <paramref name="other"/> is fully contained within this rectangle.</summary>
    public bool Contains(Rectangle other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Returns true if this rectangle overlaps <paramref name="other"/>.</summary>
    public bool Intersects(Rectangle other) =>
        other.X < Right && other.Right > X && other.Y < Bottom && other.Bottom > Y;

    public bool Equals(Rectangle other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is Rectangle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"({X}, {Y}, {Width}×{Height})";
}
