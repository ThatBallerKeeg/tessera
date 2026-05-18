using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Tiles;

namespace Engine.Core.Serialization;

/// <summary>
/// Serializes <see cref="ChunkCoord"/> as a compact comma-separated string, e.g. <c>"2,-1"</c>.
/// Also handles dictionary-key serialization via <see cref="ReadAsPropertyName"/> /
/// <see cref="WriteAsPropertyName"/>, enabling <c>Dictionary&lt;ChunkCoord, …&gt;</c>
/// to round-trip as a JSON object.
/// </summary>
internal sealed class ChunkCoordJsonConverter : JsonConverter<ChunkCoord>
{
    public override ChunkCoord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString() ?? throw new JsonException("Expected string for ChunkCoord."));

    public override void Write(Utf8JsonWriter writer, ChunkCoord value, JsonSerializerOptions options)
        => writer.WriteStringValue(Format(value));

    public override ChunkCoord ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString() ?? throw new JsonException("Expected string key for ChunkCoord."));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ChunkCoord value, JsonSerializerOptions options)
        => writer.WritePropertyName(Format(value));

    private static string Format(ChunkCoord c) => $"{c.X},{c.Y}";

    private static ChunkCoord Parse(string s)
    {
        int comma = s.IndexOf(',');
        if (comma < 0)
            throw new JsonException($"Invalid ChunkCoord '{s}': expected 'x,y'.");
        int x = int.Parse(s[..comma], CultureInfo.InvariantCulture);
        int y = int.Parse(s[(comma + 1)..], CultureInfo.InvariantCulture);
        return new ChunkCoord(x, y);
    }
}
