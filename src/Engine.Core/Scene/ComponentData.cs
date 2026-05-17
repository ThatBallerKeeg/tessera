using System.Text.Json;

namespace Engine.Core.Scene;

/// <summary>
/// On-disk representation of a Component.
/// Fields are stored as raw <see cref="JsonElement"/> values so they survive schema changes
/// and can be re-hydrated once the user's assembly is loaded at runtime.
/// </summary>
public sealed class ComponentData
{
    /// <summary>Assembly-qualified type name used to instantiate the component at runtime.</summary>
    public string TypeName { get; set; } = "";

    /// <summary>Serialized field values keyed by field name.</summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = new();
}
