using System.Text.Json;
using Engine.Core.Animation;
using Engine.Core.Math;
using Engine.Core.Sprites;

namespace Engine.Core.Serialization;

/// <summary>
/// Parses Aseprite's "Hash" JSON export format into engine-native data types.
/// </summary>
/// <remarks>
/// <para>
/// The "Hash" format stores frames as a JSON object whose keys are frame names
/// (e.g. <c>"player 0.png"</c>) and whose values contain a <c>frame</c> sub-object
/// with <c>x</c>, <c>y</c>, <c>w</c>, <c>h</c> and a top-level <c>duration</c> field.
/// A <c>meta.frameTags</c> array maps tag names to frame-index ranges (0-based) that
/// become <see cref="AnimationClip"/>s.
/// </para>
/// <para>
/// Frame ordering is the insertion order of the <c>frames</c> object.
/// <see cref="SpriteId"/>s are assigned starting at 1 (0 is <see cref="SpriteId.Empty"/>)
/// in that order.
/// </para>
/// <para>
/// <see cref="SpritesheetData.ImagePath"/> is set to the raw value of <c>meta.image</c>.
/// The caller is responsible for resolving it relative to the JSON file's directory.
/// </para>
/// </remarks>
public static class AsepriteImporter
{
    /// <summary>
    /// Parses Aseprite "Hash" JSON and returns the resulting data.
    /// </summary>
    /// <param name="json">The full text of the Aseprite JSON export.</param>
    /// <returns>
    /// A <see cref="SpritesheetData"/> containing one <see cref="SpriteFrame"/> per Aseprite
    /// frame, and one <see cref="AnimationClip"/> per <c>meta.frameTags</c> entry.
    /// If <c>meta.frameTags</c> is absent or empty, the clips list is empty.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the JSON cannot be parsed or required properties are missing.
    /// </exception>
    public static (SpritesheetData Sheet, IReadOnlyList<AnimationClip> Clips) Import(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Aseprite JSON could not be parsed: {ex.Message}", ex);
        }

        using (doc)
        {
            return ParseDocument(doc.RootElement);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static (SpritesheetData Sheet, IReadOnlyList<AnimationClip> Clips)
        ParseDocument(JsonElement root)
    {
        // ── Frames ───────────────────────────────────────────────────────────
        // The "frames" value must be a JSON object.  Keys are the Aseprite frame
        // names; insertion order is authoritative for SpriteId assignment.

        if (!root.TryGetProperty("frames", out var framesEl) ||
            framesEl.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Aseprite JSON is missing the required 'frames' object.");
        }

        // (SpriteFrame, durationMs) in declaration order.
        var frameEntries = new List<(SpriteFrame Frame, int DurationMs)>();
        int nextId = 1;   // 0 is SpriteId.Empty

        foreach (var property in framesEl.EnumerateObject())
        {
            var value = property.Value;

            if (!value.TryGetProperty("frame", out var rectEl))
                throw new InvalidDataException(
                    $"Aseprite frame '{property.Name}' is missing the 'frame' sub-object.");

            var spriteFrame = new SpriteFrame
            {
                Id   = new SpriteId(nextId++),
                Name = property.Name,
                SourceRect = new Rectangle(
                    rectEl.GetProperty("x").GetInt32(),
                    rectEl.GetProperty("y").GetInt32(),
                    rectEl.GetProperty("w").GetInt32(),
                    rectEl.GetProperty("h").GetInt32()),
                Pivot = Vector2.Zero,
            };

            int durationMs = value.TryGetProperty("duration", out var durEl)
                ? durEl.GetInt32()
                : 100;   // Aseprite default when not exported

            frameEntries.Add((spriteFrame, durationMs));
        }

        // ── Meta ─────────────────────────────────────────────────────────────

        if (!root.TryGetProperty("meta", out var metaEl))
            throw new InvalidDataException("Aseprite JSON is missing the required 'meta' object.");

        string imagePath = metaEl.TryGetProperty("image", out var imgEl)
            ? imgEl.GetString() ?? ""
            : "";

        string sheetName = System.IO.Path.GetFileNameWithoutExtension(imagePath);
        if (string.IsNullOrEmpty(sheetName)) sheetName = "spritesheet";

        // ── SpritesheetData ───────────────────────────────────────────────────

        var sheet = new SpritesheetData
        {
            Name      = sheetName,
            ImagePath = imagePath,
        };

        foreach (var (frame, _) in frameEntries)
            sheet.Frames.Add(frame);

        // ── AnimationClips from frameTags ─────────────────────────────────────

        var clips = new List<AnimationClip>();

        if (metaEl.TryGetProperty("frameTags", out var tagsEl) &&
            tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.EnumerateArray())
            {
                string clipName = tag.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString() ?? ""
                    : "";

                int from = tag.TryGetProperty("from", out var fromEl) ? fromEl.GetInt32() : 0;
                int to   = tag.TryGetProperty("to",   out var toEl)   ? toEl.GetInt32()   : 0;

                var clip = new AnimationClip
                {
                    Name  = clipName,
                    Loops = true,
                };

                for (int i = from; i <= to && i < frameEntries.Count; i++)
                {
                    var (frame, durationMs) = frameEntries[i];
                    clip.Frames.Add(new AnimationClipFrame
                    {
                        SpriteId   = frame.Id,
                        DurationMs = durationMs,
                    });
                }

                clips.Add(clip);
            }
        }

        return (sheet, clips);
    }
}
