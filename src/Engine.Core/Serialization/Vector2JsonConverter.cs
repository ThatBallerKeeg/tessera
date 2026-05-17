using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Math;

namespace Engine.Core.Serialization;

/// <summary>
/// Serializes <see cref="Vector2"/> as a compact comma-separated string, e.g. <c>"1.5,2.5"</c>.
/// Values are written in invariant culture to guarantee portability across locales.
/// </summary>
internal sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string raw = reader.GetString()
            ?? throw new JsonException("Expected a string value for Vector2.");

        string[] parts = raw.Split(',');
        if (parts.Length != 2)
            throw new JsonException($"Invalid Vector2 format '{raw}': expected 'x,y'.");

        return new Vector2(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            $"{value.X.ToString(CultureInfo.InvariantCulture)},{value.Y.ToString(CultureInfo.InvariantCulture)}");
    }
}
