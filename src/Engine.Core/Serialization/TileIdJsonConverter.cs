using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Tiles;

namespace Engine.Core.Serialization;

/// <summary>Serializes <see cref="TileId"/> as a compact JSON integer, e.g. <c>42</c>.</summary>
internal sealed class TileIdJsonConverter : JsonConverter<TileId>
{
    public override TileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, TileId value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}
