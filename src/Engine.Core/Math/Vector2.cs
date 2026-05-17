namespace Engine.Core.Math;

/// <summary>Immutable 2D vector with <see cref="float"/> components.</summary>
public readonly struct Vector2 : IEquatable<Vector2>
{
    /// <summary>Horizontal component.</summary>
    public float X { get; }

    /// <summary>Vertical component.</summary>
    public float Y { get; }

    /// <summary>Creates a vector with the given components.</summary>
    public Vector2(float x, float y) { X = x; Y = y; }

    /// <summary>Vector with both components set to zero.</summary>
    public static readonly Vector2 Zero = new(0f, 0f);

    /// <summary>Vector with both components set to one.</summary>
    public static readonly Vector2 One = new(1f, 1f);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);
    public static Vector2 operator *(Vector2 v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2 operator *(float scalar, Vector2 v) => new(v.X * scalar, v.Y * scalar);
    public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
    public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);

    /// <summary>Returns the dot product of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>Returns the Euclidean distance between <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>.</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}
