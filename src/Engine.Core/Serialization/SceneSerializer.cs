using System.Text.Json;
using Engine.Core.Scene;

namespace Engine.Core.Serialization;

/// <summary>Saves and loads <see cref="SceneData"/> to and from JSON streams.</summary>
public static class SceneSerializer
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new Vector2JsonConverter(), new Vector2IntJsonConverter() },
    };

    /// <summary>Serializes <paramref name="scene"/> to <paramref name="stream"/> as indented camelCase JSON.</summary>
    public static void Save(SceneData scene, Stream stream)
    {
        JsonSerializer.Serialize(stream, scene, Options);
    }

    /// <summary>
    /// Deserializes a <see cref="SceneData"/> from <paramref name="stream"/>.
    /// </summary>
    /// <exception cref="SceneFormatException">
    /// Thrown when the file's <c>formatVersion</c> exceeds <see cref="CurrentVersion"/>,
    /// or when the file cannot be parsed.
    /// </exception>
    public static SceneData Load(Stream stream)
    {
        SceneData scene;
        try
        {
            scene = JsonSerializer.Deserialize<SceneData>(stream, Options)
                ?? throw new SceneFormatException("Scene file is empty.");
        }
        catch (JsonException ex)
        {
            throw new SceneFormatException($"Scene file could not be parsed: {ex.Message}", ex);
        }

        if (scene.FormatVersion > CurrentVersion)
            throw new SceneFormatException(
                $"Scene format version {scene.FormatVersion} is not supported by this engine " +
                $"(maximum supported: {CurrentVersion}).");

        if (scene.FormatVersion < CurrentVersion)
            Migrate(scene, scene.FormatVersion);

        return scene;
    }

    // Stub: add version-specific migration steps here as the format evolves.
    private static void Migrate(SceneData scene, int fromVersion)
    {
        scene.FormatVersion = CurrentVersion;
    }
}
