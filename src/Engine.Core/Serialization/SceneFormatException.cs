namespace Engine.Core.Serialization;

/// <summary>
/// Thrown when a scene file cannot be loaded because its <c>formatVersion</c> is
/// newer than the current engine supports, or because the file is structurally invalid.
/// </summary>
public sealed class SceneFormatException : Exception
{
    /// <inheritdoc cref="Exception(string)"/>
    public SceneFormatException(string message) : base(message) { }

    /// <inheritdoc cref="Exception(string, Exception)"/>
    public SceneFormatException(string message, Exception inner) : base(message, inner) { }
}
