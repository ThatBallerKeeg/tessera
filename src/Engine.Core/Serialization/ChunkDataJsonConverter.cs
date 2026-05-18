using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Tiles;

namespace Engine.Core.Serialization;

/// <summary>
/// Serializes <see cref="ChunkData"/> as a base64 string containing the raw tile array.
/// Each <see cref="TileId"/> is packed as 4 little-endian bytes; a full 16×16 chunk is
/// 1 024 bytes → ~1 368 base64 characters, far less than 256 individual JSON integers.
/// </summary>
internal sealed class ChunkDataJsonConverter : JsonConverter<ChunkData>
{
    private const int TileCount = ChunkCoord.TileCount;  // 256
    private const int ByteCount = TileCount * 4;          // 1 024

    public override ChunkData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!reader.TryGetBytesFromBase64(out byte[]? bytes) || bytes is null)
            throw new JsonException("Expected a base64 string for ChunkData.");

        if (bytes.Length != ByteCount)
            throw new JsonException($"ChunkData base64 must decode to {ByteCount} bytes; got {bytes.Length}.");

        var chunk = new ChunkData();
        int nonEmpty = 0;
        for (int i = 0; i < TileCount; i++)
        {
            int v = bytes[i * 4]
                  | (bytes[i * 4 + 1] << 8)
                  | (bytes[i * 4 + 2] << 16)
                  | (bytes[i * 4 + 3] << 24);
            chunk.Tiles[i] = new TileId(v);
            if (v != 0) nonEmpty++;
        }
        chunk.NonEmptyCount = nonEmpty;
        return chunk;
    }

    public override void Write(Utf8JsonWriter writer, ChunkData value, JsonSerializerOptions options)
    {
        var bytes = new byte[ByteCount];
        for (int i = 0; i < TileCount; i++)
        {
            int v = value.Tiles[i].Value;
            bytes[i * 4 + 0] = (byte)(v         & 0xFF);
            bytes[i * 4 + 1] = (byte)((v >>  8) & 0xFF);
            bytes[i * 4 + 2] = (byte)((v >> 16) & 0xFF);
            bytes[i * 4 + 3] = (byte)((v >> 24) & 0xFF);
        }
        writer.WriteBase64StringValue(bytes);
    }
}
