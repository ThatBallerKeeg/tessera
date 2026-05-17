namespace Engine.Core.Math;

/// <summary>Immutable 2D vector with <see cref="int"/> components.</summary>
public readonly struct Vector2Int : IEquatable<Vector2Int>
{
    /// <summary>Horizontal component.</summary>
    public int X { get; }

    /// <summary>Vertical component.</summary>
    public int Y { get; }

    /// <summary>Creates a vector with the given components.</summary>
    public Vector2Int(int x, int y) { X = x; Y = y; }

    /// <summary>Vector with both components set to zero.</summary>
    public static readonly Vector2Int Zero = new(0, 0);

    /// <summary>Vector with both components set to one.</summary>
    public static readonly Vector2Int One = new(1, 1);

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2Int operator -(Vector2Int v) => new(-v.X, -v.Y);
    public static Vector2Int operator *(Vector2Int v, int scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2Int operator *(int scalar, Vector2Int v) => new(v.X * scalar, v.Y * scalar);
    public static bool operator ==(Vector2Int a, Vector2Int b) => a.Equals(b);
    public static bool operator !=(Vector2Int a, Vector2Int b) => !a.Equals(b);

    /// <summary>Returns the integer dot product of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static int Dot(Vector2Int a, Vector2Int b) => a.X * b.X + a.Y * b.Y;

    /// <summary>Returns the Euclidean distance between <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Distance(Vector2Int a, Vector2Int b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>,
    /// rounding each component to the nearest integer.
    /// </summary>
    public static Vector2Int Lerp(Vector2Int a, Vector2Int b, float t) =>
        new((int)MathF.Round(a.X + (b.X - a.X) * t),
            (int)MathF.Round(a.Y + (b.Y - a.Y) * t));

    public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2Int other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}
