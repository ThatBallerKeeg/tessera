# Tessera Engine

A 2D pixel-art game engine for top-down RPGs, roguelikes, and sandbox games. Real-time movement (Zelda / Stardew Valley style), C# scripting, and support for procedurally-generated bounded worlds. Targets Windows, macOS, and Linux.

## Architecture

Three projects with a strict one-way dependency chain:

```
Engine.Core  ←  Engine.Runtime  ←  Engine.Editor
                      ↑
               User game projects
```

| Project | Purpose |
|---|---|
| `src/Engine.Core` | Pure data and logic — math types, scene/GameObject/Component data model, serialization. No MonoGame, no Avalonia. |
| `src/Engine.Runtime` | MonoGame-based runtime embedded in user games — game loop, rendering, input, audio, physics. |
| `src/Engine.Editor` | Avalonia desktop editor — scene authoring, inspector, asset tools. |

User games are separate `.csproj` projects that reference `Engine.Core` and `Engine.Runtime` via NuGet.

## Tech Stack

- .NET 10 / C#
- [MonoGame.Framework.DesktopGL](https://www.monogame.net/) 3.8.4.1 — cross-platform rendering and input
- [Avalonia](https://avaloniaui.net/) 11.x — desktop editor UI
- `System.Text.Json` — scene serialization
- Roslyn (`Microsoft.CodeAnalysis.CSharp`) — live C# script compilation (Phase 3)
- xUnit + FluentAssertions — tests

## Getting Started

```bash
# Build everything
dotnet build

# Run all tests
dotnet test

# Launch the editor
dotnet run --project src/Engine.Editor
```

## Project Status

Currently in **Phase 0 — Foundation**. The solution skeleton is in place; no rendering or gameplay yet.

| Phase | Goal | Status |
|---|---|---|
| 0 — Foundation | Solution scaffold, scene data model, serialization, editor shell | In progress |
| 1 — Tilemap | Chunked tilemap, autotiling, animated tiles, procgen utilities | Planned |
| 2 — Sprites & Animation | Spritesheets, animation clips, Aseprite import | Planned |
| 3 — Scripting & Inspector | Hot-reload C# components, reflection inspector, prefabs | Planned |
| 4 — Gameplay Essentials | AABB collision, camera, input, audio, object pooling | Planned |
| 5 — RPG Systems | Dialog, pathfinding, inventory, save/load | Planned |
| 6 — Public Release | Build pipeline, docs, sample project | Planned |

See [`PLAN.md`](PLAN.md) for full phase specs and acceptance checklists.

## Repository Layout

```
/
├── .editorconfig
├── Directory.Build.props    # Nullable, TreatWarningsAsErrors, ImplicitUsings
├── Engine.sln
├── docs/
│   └── phase-0-foundation.md
├── src/
│   ├── Engine.Core/
│   ├── Engine.Runtime/
│   └── Engine.Editor/
└── tests/
    ├── Engine.Core.Tests/
    └── Engine.Runtime.Tests/
```

## Hard Invariants

1. `Engine.Core` references no rendering or UI library — enforced by a build-time test.
2. Every scene type round-trips through JSON serialization — each new type gets a round-trip test.
3. Scene files carry `"formatVersion": N` — loaders migrate old versions forward, never silently break saves.
4. No statics or singletons in the runtime — everything is constructable in tests.
5. Deterministic simulation — fixed-step tick, seedable RNG.
