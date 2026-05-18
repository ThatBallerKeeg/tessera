namespace Engine.Core.Tiles;

/// <summary>Immutable identifier for a tile. Value 0 is the empty/air tile by convention.</summary>
public readonly struct TileId : IEquatable<TileId>
{
    /// <summary>Raw integer value. 0 = empty.</summary>
    public int Value { get; }

    /// <summary>The empty (air) tile.</summary>
    public static readonly TileId Empty = new(0);

    /// <summary>Creates a <see cref="TileId"/> with the given value.</summary>
    public TileId(int value) { Value = value; }

    public bool Equals(TileId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is TileId other && Equals(other);
    public override int GetHashCode() => Value;
    public static bool operator ==(TileId a, TileId b) => a.Equals(b);
    public static bool operator !=(TileId a, TileId b) => !a.Equals(b);
    public override string ToString() => $"TileId({Value})";
}
