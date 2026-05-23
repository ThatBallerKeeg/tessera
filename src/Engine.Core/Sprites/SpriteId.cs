namespace Engine.Core.Sprites;

/// <summary>
/// Stable identity for a single <see cref="SpriteFrame"/> within a <see cref="SpritesheetData"/>.
/// Value 0 is reserved as the sentinel "no sprite" (<see cref="Empty"/>);
/// authoring tools assign 1-based IDs.
/// </summary>
public readonly struct SpriteId : IEquatable<SpriteId>
{
    /// <summary>The underlying integer identifier.</summary>
    public int Value { get; }

    /// <summary>Creates a SpriteId with the given value.</summary>
    public SpriteId(int value) => Value = value;

    /// <summary>Sentinel "no sprite" value (Value = 0).</summary>
    public static SpriteId Empty => new(0);

    public bool Equals(SpriteId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is SpriteId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();

    public static bool operator ==(SpriteId a, SpriteId b) => a.Value == b.Value;
    public static bool operator !=(SpriteId a, SpriteId b) => a.Value != b.Value;
}
