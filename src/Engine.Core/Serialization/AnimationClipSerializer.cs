using System.Text.Json;
using Engine.Core.Animation;

namespace Engine.Core.Serialization;

/// <summary>
/// Saves and loads <see cref="AnimationClip"/> to and from JSON streams.
/// Uses the same <see cref="SceneSerializer.Options"/> as <see cref="SceneSerializer"/>
/// and <see cref="SpritesheetSerializer"/> (camelCase, compact custom converters for all
/// Core math and ID types) so all formats share the same wire encoding.
/// </summary>
public static class AnimationClipSerializer
{
    // ── Stream-based API (primary) ────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="clip"/> to <paramref name="stream"/> as indented camelCase JSON.
    /// </summary>
    public static void Save(AnimationClip clip, Stream stream)
        => JsonSerializer.Serialize(stream, clip, SceneSerializer.Options);

    /// <summary>
    /// Deserializes an <see cref="AnimationClip"/> from <paramref name="stream"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the stream cannot be parsed.</exception>
    public static AnimationClip Load(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<AnimationClip>(stream, SceneSerializer.Options)
                ?? throw new InvalidDataException("AnimationClip JSON is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"AnimationClip JSON could not be parsed: {ex.Message}", ex);
        }
    }

    // ── File-based convenience overloads ──────────────────────────────────────

    /// <summary>Saves <paramref name="clip"/> to the file at <paramref name="path"/>.</summary>
    public static void Save(AnimationClip clip, string path)
    {
        using var fs = File.Create(path);
        Save(clip, fs);
    }

    /// <summary>Loads an <see cref="AnimationClip"/> from the file at <paramref name="path"/>.</summary>
    public static AnimationClip Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }
}
