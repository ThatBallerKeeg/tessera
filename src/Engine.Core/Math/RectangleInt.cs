namespace Engine.Core.Math;

/// <summary>
/// Immutable axis-aligned rectangle with <see cref="int"/> components.
/// Stored as four ints: X, Y (top-left corner), Width, Height.
/// </summary>
public readonly struct RectangleInt : IEquatable<RectangleInt>
{
    /// <summary>X coordinate of the left edge.</summary>
    public int X { get; }

    /// <summary>Y coordinate of the top edge.</summary>
    public int Y { get; }

    /// <summary>Width of the rectangle.</summary>
    public int Width { get; }

    /// <summary>Height of the rectangle.</summary>
    public int Height { get; }

    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;

    /// <summary>Top-left corner as a <see cref="Vector2Int"/>.</summary>
    public Vector2Int Position => new(X, Y);

    /// <summary>Width and height as a <see cref="Vector2Int"/>.</summary>
    public Vector2Int Size => new(Width, Height);

    /// <summary>Center point using integer division.</summary>
    public Vector2Int Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>Creates a rectangle from explicit components.</summary>
    public RectangleInt(int x, int y, int width, int height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    /// <summary>Creates a rectangle from a position and size.</summary>
    public RectangleInt(Vector2Int position, Vector2Int size)
        : this(position.X, position.Y, size.X, size.Y) { }

    /// <summary>Rectangle at the origin with zero size.</summary>
    public static readonly RectangleInt Zero = new(0, 0, 0, 0);

    /// <summary>Unit rectangle: origin, 1×1.</summary>
    public static readonly RectangleInt One = new(0, 0, 1, 1);

    /// <summary>Returns this rectangle translated by <paramref name="offset"/>.</summary>
    public static RectangleInt operator +(RectangleInt r, Vector2Int offset) =>
        new(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height);

    /// <summary>Returns this rectangle translated by the negation of <paramref name="offset"/>.</summary>
    public static RectangleInt operator -(RectangleInt r, Vector2Int offset) =>
        new(r.X - offset.X, r.Y - offset.Y, r.Width, r.Height);

    /// <summary>Returns this rectangle with all components scaled by <paramref name="scalar"/>.</summary>
    public static RectangleInt operator *(RectangleInt r, int scalar) =>
        new(r.X * scalar, r.Y * scalar, r.Width * scalar, r.Height * scalar);

    public static bool operator ==(RectangleInt a, RectangleInt b) => a.Equals(b);
    public static bool operator !=(RectangleInt a, RectangleInt b) => !a.Equals(b);

    /// <summary>
    /// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>,
    /// rounding each component to the nearest integer.
    /// </summary>
    public static RectangleInt Lerp(RectangleInt a, RectangleInt b, float t) =>
        new((int)MathF.Round(a.X + (b.X - a.X) * t),
            (int)MathF.Round(a.Y + (b.Y - a.Y) * t),
            (int)MathF.Round(a.Width + (b.Width - a.Width) * t),
            (int)MathF.Round(a.Height + (b.Height - a.Height) * t));

    /// <summary>Returns the Euclidean distance between the centers of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Distance(RectangleInt a, RectangleInt b)
    {
        float cx = (a.X + a.Width / 2f) - (b.X + b.Width / 2f);
        float cy = (a.Y + a.Height / 2f) - (b.Y + b.Height / 2f);
        return MathF.Sqrt(cx * cx + cy * cy);
    }

    /// <summary>Returns true if <paramref name="point"/> lies within or on the boundary of this rectangle.</summary>
    public bool Contains(Vector2Int point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>Returns true if <paramref name="other"/> is fully contained within this rectangle.</summary>
    public bool Contains(RectangleInt other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Returns true if this rectangle overlaps <paramref name="other"/>.</summary>
    public bool Intersects(RectangleInt other) =>
        other.X < Right && other.Right > X && other.Y < Bottom && other.Bottom > Y;

    public bool Equals(RectangleInt other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is RectangleInt other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"({X}, {Y}, {Width}×{Height})";
}
