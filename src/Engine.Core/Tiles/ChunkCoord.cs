using Engine.Core.Math;

namespace Engine.Core.Tiles;

/// <summary>
/// Grid coordinate identifying a 16×16 tile chunk.
/// Chunk (X, Y) covers world tiles [X*ChunkSize, X*ChunkSize+15] × [Y*ChunkSize, Y*ChunkSize+15].
/// Negative world coordinates are supported via floor division.
/// </summary>
public readonly struct ChunkCoord : IEquatable<ChunkCoord>
{
    /// <summary>Chunk column.</summary>
    public int X { get; }

    /// <summary>Chunk row.</summary>
    public int Y { get; }

    /// <summary>Width and height of each chunk in tiles.</summary>
    public const int ChunkSize = 16;

    /// <summary>Total number of tiles in one chunk (ChunkSize²).</summary>
    public const int TileCount = ChunkSize * ChunkSize;

    /// <summary>Creates a chunk coordinate.</summary>
    public ChunkCoord(int x, int y) { X = x; Y = y; }

    /// <summary>Returns the chunk that contains <paramref name="worldPos"/>.</summary>
    public static ChunkCoord FromWorld(Vector2Int worldPos) =>
        new(FloorDiv(worldPos.X, ChunkSize), FloorDiv(worldPos.Y, ChunkSize));

    /// <summary>
    /// Returns the flat index (0–255) of <paramref name="worldPos"/> within its chunk.
    /// Index = localY * ChunkSize + localX.
    /// </summary>
    public static int LocalIndex(Vector2Int worldPos)
    {
        var coord = FromWorld(worldPos);
        int lx = worldPos.X - coord.X * ChunkSize;
        int ly = worldPos.Y - coord.Y * ChunkSize;
        return ly * ChunkSize + lx;
    }

    // Floor division that handles negative numerators correctly.
    // C# truncates towards zero; we need towards negative infinity.
    private static int FloorDiv(int a, int b) =>
        a / b + (a % b != 0 && (a ^ b) < 0 ? -1 : 0);

    public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
    public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);

    /// <summary>Returns "X,Y" — the wire format used as a JSON dictionary key.</summary>
    public override string ToString() => $"{X},{Y}";
}
