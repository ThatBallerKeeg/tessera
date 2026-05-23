using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Sprites;

namespace Engine.Core.Serialization;

/// <summary>Serializes <see cref="SpriteId"/> as a compact JSON integer, e.g. <c>7</c>.</summary>
internal sealed class SpriteIdJsonConverter : JsonConverter<SpriteId>
{
    public override SpriteId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, SpriteId value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}
