using Engine.Core.Scene;
using Engine.Runtime.Components;

namespace Engine.Runtime.Entities;

/// <summary>
/// Instantiates a live <see cref="Scene"/> from a <see cref="SceneData"/> snapshot.
/// </summary>
/// <remarks>
/// Component instantiation is currently a hardcoded switch on <see cref="ComponentData.TypeName"/>.
/// Phase 3 replaces this seam with reflection-based lookup of user-scripted components via
/// <c>AssemblyLoadContext</c>.  Adding a new engine-builtin component only requires a new
/// case in <see cref="InstantiateComponent"/>.
/// </remarks>
public static class SceneLoader
{
    /// <summary>
    /// Builds a live <see cref="Scene"/> from <paramref name="data"/>.
    /// Tilemap layers are passed through as data only; renderers build their own contexts.
    /// Unknown component type names are silently skipped with a console warning.
    /// </summary>
    public static Scene Load(SceneData data)
    {
        var layers = data.Tilemaps
            .OrderBy(static t => t.LayerIndex);

        var scene = new Scene(layers);

        foreach (var gd in data.GameObjects)
        {
            var go = new GameObject(gd.Id, gd.Name) { Position = gd.Position };

            foreach (var cd in gd.Components)
            {
                var component = InstantiateComponent(cd);
                if (component is null)
                {
                    Console.WriteLine(
                        $"[SceneLoader] Unknown component type '{cd.TypeName}' on " +
                        $"'{gd.Name}' — skipped. (Phase 3 wires user-scripted types.)");
                    continue;
                }
                go.AddComponent(component);
            }

            scene.AddGameObject(go);
        }

        return scene;
    }

    // ── Component factory — Phase 3 replaces this with reflection ─────────────

    private static Component? InstantiateComponent(ComponentData data) =>
        data.TypeName switch
        {
            "SpriteRenderer" => new SpriteRenderer(),
            "Animator"       => new Animator(),
            _                => null,
        };
}
