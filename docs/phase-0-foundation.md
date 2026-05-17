# Phase 0 — Foundation

End-to-end skeleton. By the end of this phase: solution builds, tests run, the editor opens, an empty scene saves and loads.

## Tasks

### 0.1 Solution scaffolding

Create this layout:

```
/
├── .editorconfig            # file-scoped namespaces, 4-space indent, nullable enabled
├── Directory.Build.props    # common .NET 10 settings, warnings-as-errors, nullable enabled, implicit usings
├── Engine.sln
├── src/
│   ├── Engine.Core/         # net10.0, NO MonoGame, NO Avalonia
│   ├── Engine.Runtime/      # net10.0, references Engine.Core + MonoGame 3.8.4.1
│   └── Engine.Editor/       # net10.0, references Engine.Core + Engine.Runtime + Avalonia 11.x
└── tests/
    ├── Engine.Core.Tests/
    └── Engine.Runtime.Tests/
```

Set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<Nullable>enable</Nullable>` in `Directory.Build.props`.

### 0.2 Architecture invariant check

Add a test in `Engine.Core.Tests` that loads `Engine.Core.dll` via `System.Reflection.Metadata` (don't `Assembly.Load` — read the manifest only) and asserts no referenced assembly name starts with `MonoGame` or `Avalonia`. Fail the test with a clear message naming the offending reference.

### 0.3 Engine.Core math

Implement these as immutable `readonly struct`s in `Engine.Core/Math/`:

- `Vector2` (two `float`s)
- `Vector2Int` (two `int`s)
- `Rectangle` (Vector2 position + Vector2 size, or four floats — pick one and document)
- `RectangleInt`

Each gets: operators (`+`, `-`, `*` by scalar), equality, GetHashCode, common statics (`Lerp`, `Distance`, `Dot`, `Zero`, `One`). Tests for each operation.

Do **not** reuse `Microsoft.Xna.Framework.Vector2` here — Engine.Core can't reference MonoGame. Provide conversion extension methods on the runtime side in Phase 1.

### 0.4 Scene data model

In `Engine.Core/Scene/`:

```csharp
public sealed class SceneData
{
    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled";
    public List<GameObjectData> GameObjects { get; set; } = new();
}

public sealed class GameObjectData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "GameObject";
    public Vector2 Position { get; set; }
    public List<ComponentData> Components { get; set; } = new();
}

public sealed class ComponentData
{
    public string TypeName { get; set; } = "";
    public Dictionary<string, JsonElement> Fields { get; set; } = new();
}
```

These are the **on-disk** shapes. The live runtime types (with behavior) come in Phase 3.

### 0.5 Serialization

In `Engine.Core/Serialization/`:

- `SceneSerializer` with `Save(SceneData, Stream)` and `Load(Stream) -> SceneData`.
- Custom `JsonConverter<Vector2>` and `JsonConverter<Vector2Int>` (compact form like `"1.5,2.5"` is fine, or an object — pick one, document it).
- Throw `SceneFormatException` on `FormatVersion > CURRENT_VERSION`. For older versions, run a migration function (stub: just bump the version to current).
- Use `JsonSerializerOptions` with indented output, camelCase property naming.

Tests:
- Round-trip an empty scene.
- Round-trip a scene with two GameObjects each having one component with mixed field types (int, string, float, Vector2).
- Loading a scene with `FormatVersion = 999` throws `SceneFormatException`.

### 0.6 Engine.Runtime minimal game

In `Engine.Runtime/`:

- `EngineGame : Microsoft.Xna.Framework.Game` — opens a 1280×720 window, clears to `new Color(20, 20, 28)`.
- `LoadScene(SceneData)` method that walks the scene and creates internal placeholder representations of GameObjects. Don't render anything yet — just prove the runtime can consume Core data without crashing.
- Console-print the scene name and GameObject count on load (temporary; will be removed in Phase 1).

### 0.7 Engine.Editor shell

Avalonia 11 app in `Engine.Editor/`:

- Main window, 1400×900, dark theme.
- Placeholder docked panels: "Scene", "Inspector", "Viewport", "Assets". Use a `Grid` with `GridSplitter`s for now — proper docking (Dock.Avalonia or similar) can come later.
- Menu: File > New Scene / Open Scene / Save Scene / Exit.
- New Scene shows a "Scene" panel with the scene name editable.
- Save uses `SceneSerializer.Save`. Open uses `SceneSerializer.Load`.
- The Viewport panel is a placeholder gray rectangle for now. **MonoGame-in-Avalonia integration is deferred to Phase 1** with a research budget — Phase 0 needs only the UI shell.

### 0.8 CI

GitHub Actions workflow `.github/workflows/ci.yml`:

```yaml
name: CI
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build
```

Test that this passes on a clean clone.

## Acceptance checklist

- [ ] `dotnet build` clean on a fresh clone.
- [ ] `dotnet test` green.
- [ ] `dotnet run --project src/Engine.Editor` opens the editor window.
- [ ] File > New Scene → File > Save Scene writes a valid JSON file with `"formatVersion": 1`.
- [ ] File > Open Scene reads that file back; the scene name appears in the Scene panel.
- [ ] Invariant check fails the build if MonoGame or Avalonia is added to Engine.Core.
- [ ] CI is green on push.

## What's NOT in this phase

- Rendering anything from a scene.
- MonoGame-in-Avalonia viewport integration (Phase 1).
- The GameObject/Component **runtime behavior** — only the data shapes exist here.
- Asset system. Scenes reference no assets yet.
- Hot reload, scripting, or anything Roslyn-related (Phase 3).
- Anything pixel-art-specific. Phase 0 is pure scaffolding.

## Suggested commit order

1. Solution + Directory.Build.props + .editorconfig
2. Empty project skeletons + project references
3. Invariant check test
4. Math types + tests
5. Scene data model
6. Serializer + tests
7. Minimal Engine.Runtime Game
8. Avalonia editor shell + menus
9. CI workflow

Each commit should build and test cleanly.
