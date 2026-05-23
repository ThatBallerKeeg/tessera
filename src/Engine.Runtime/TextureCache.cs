using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime;

/// <summary>
/// Loads and caches <see cref="Texture2D"/> instances by file path so that multiple
/// components referencing the same image share a single GPU upload.
/// </summary>
/// <remarks>
/// Create one <see cref="TextureCache"/> per <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/>
/// context (i.e. one per <c>EngineGame</c> instance) and inject it into anything that needs
/// to resolve a texture from a path.  Never create a second cache for the same device —
/// that wastes VRAM and defeats deduplication.
/// </remarks>
public sealed class TextureCache : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Creates a cache backed by <paramref name="device"/>.
    /// The <paramref name="device"/> must remain valid for the lifetime of this cache.
    /// </summary>
    public TextureCache(GraphicsDevice device) => _device = device;

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Texture2D"/> for <paramref name="path"/>,
    /// loading and caching it on first access.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Propagated from <see cref="File.OpenRead"/> when the file does not exist.
    /// </exception>
    public Texture2D Get(string path)
    {
        if (_entries.TryGetValue(path, out var cached))
            return cached;

        using var stream = File.OpenRead(path);
        var tex = Texture2D.FromStream(_device, stream);
        _entries[path] = tex;
        return tex;
    }

    /// <summary>
    /// Returns <see langword="true"/> and sets <paramref name="texture"/> if
    /// <paramref name="path"/> has already been loaded.
    /// </summary>
    public bool TryGet(string path, out Texture2D? texture)
        => _entries.TryGetValue(path, out texture);

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>Disposes all cached textures.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var tex in _entries.Values)
            tex.Dispose();
        _entries.Clear();
    }
}
