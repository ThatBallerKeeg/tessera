using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Math;

namespace Engine.Core.Serialization;

/// <summary>
/// Serializes <see cref="Vector2Int"/> as a compact comma-separated string, e.g. <c>"1,2"</c>.
/// </summary>
internal sealed class Vector2IntJsonConverter : JsonConverter<Vector2Int>
{
    public override Vector2Int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string raw = reader.GetString()
            ?? throw new JsonException("Expected a string value for Vector2Int.");

        string[] parts = raw.Split(',');
        if (parts.Length != 2)
            throw new JsonException($"Invalid Vector2Int format '{raw}': expected 'x,y'.");

        return new Vector2Int(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, Vector2Int value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.X},{value.Y}");
    }
}
