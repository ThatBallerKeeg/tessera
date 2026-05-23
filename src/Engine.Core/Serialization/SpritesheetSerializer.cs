using System.Text.Json;
using Engine.Core.Sprites;

namespace Engine.Core.Serialization;

/// <summary>
/// Saves and loads <see cref="SpritesheetData"/> to and from JSON streams.
/// Uses the same <see cref="JsonSerializerOptions"/> as <see cref="SceneSerializer"/>
/// (camelCase, compact custom converters for all Core math types) so the two formats
/// share the same wire encoding.
/// </summary>
public static class SpritesheetSerializer
{
    // ── Stream-based API (primary) ────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="data"/> to <paramref name="stream"/> as indented camelCase JSON.
    /// </summary>
    public static void Save(SpritesheetData data, Stream stream)
        => JsonSerializer.Serialize(stream, data, SceneSerializer.Options);

    /// <summary>
    /// Deserializes a <see cref="SpritesheetData"/> from <paramref name="stream"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the stream cannot be parsed.</exception>
    public static SpritesheetData Load(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<SpritesheetData>(stream, SceneSerializer.Options)
                ?? throw new InvalidDataException("Spritesheet JSON is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Spritesheet JSON could not be parsed: {ex.Message}", ex);
        }
    }

    // ── File-based convenience overloads ──────────────────────────────────────

    /// <summary>Saves <paramref name="data"/> to the file at <paramref name="path"/>.</summary>
    public static void Save(SpritesheetData data, string path)
    {
        using var fs = File.Create(path);
        Save(data, fs);
    }

    /// <summary>Loads a <see cref="SpritesheetData"/> from the file at <paramref name="path"/>.</summary>
    public static SpritesheetData Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }
}
