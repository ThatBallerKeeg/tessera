using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Math;

namespace Engine.Core.Serialization;

/// <summary>
/// Serializes <see cref="Rectangle"/> as a compact comma-separated string,
/// e.g. <c>"32,16,16,16"</c> (x,y,width,height).
/// Values are written in invariant culture to guarantee portability across locales.
/// </summary>
internal sealed class RectangleJsonConverter : JsonConverter<Rectangle>
{
    public override Rectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string raw = reader.GetString()
            ?? throw new JsonException("Expected a string value for Rectangle.");

        string[] parts = raw.Split(',');
        if (parts.Length != 4)
            throw new JsonException($"Invalid Rectangle format '{raw}': expected 'x,y,width,height'.");

        return new Rectangle(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture),
            float.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, Rectangle value, JsonSerializerOptions options)
    {
        // Emit integer values without a decimal point when the floats are whole numbers,
        // keeping the JSON readable for the common pixel-coordinate case.
        static string Fmt(float f) =>
            f == MathF.Floor(f)
                ? ((int)f).ToString(CultureInfo.InvariantCulture)
                : f.ToString(CultureInfo.InvariantCulture);

        writer.WriteStringValue($"{Fmt(value.X)},{Fmt(value.Y)},{Fmt(value.Width)},{Fmt(value.Height)}");
    }
}
