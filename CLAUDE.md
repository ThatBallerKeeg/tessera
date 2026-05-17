# CLAUDE.md

Persistent context for Claude Code sessions on this project. Read this at the start of every session before writing any code.

## Project

A 2D pixel-art game engine specialized for top-down RPGs, roguelikes, and sandbox games. Real-time movement (Zelda / Stardew Valley style — not turn-based). Supports procedurally-generated bounded worlds (Core Keeper / Necesse-scale, not infinite Minecraft-scale). Users write game logic as C# `Component` subclasses that compile into their game. Targeted for public release across Windows, macOS, and Linux.

## Architecture

Three projects in one solution. Dependency direction is strict — violations should fail the build.

```
Engine.Core  ←  Engine.Runtime  ←  Engine.Editor
                       ↑
                User game projects
```

- **`src/Engine.Core/`** — Pure data and logic. **NO** `MonoGame.*` references. **NO** `Avalonia.*` references. Contains: math primitives, the Scene/GameObject/Component *data* model (not the runtime types), serialization, asset references, tilemap data, animation clip data.
- **`src/Engine.Runtime/`** — MonoGame-based runtime that ships embedded inside users' built games. Game loop, rendering, input, audio, AABB physics, the live Component runtime, scene loader. Depends on Engine.Core + MonoGame.
- **`src/Engine.Editor/`** — Avalonia desktop app. Hosts Engine.Runtime in viewport panels. All authoring tools live here. Depends on Engine.Core + Engine.Runtime + Avalonia.

User games are separate `.csproj` projects that NuGet-reference Engine.Core + Engine.Runtime. The editor compiles user scripts via Roslyn into a collectible `AssemblyLoadContext` for hot-reload.

## Tech stack (pin these)

- .NET 10
- MonoGame.Framework.DesktopGL 3.8.4.1
- Avalonia 11.x
- `System.Text.Json` (NOT Newtonsoft.Json) with custom `JsonConverter`s
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) for compiling user scripts
- xUnit + AwesomeAssertions for tests (MIT-licensed community fork of FluentAssertions v7)

When introducing a new dependency, justify it in the commit. Smaller surface area = easier to maintain a public release.

## Hard invariants

These must hold. Verify in CI where possible.

1. `Engine.Core` references no rendering or UI library. Enforce with a build-time assembly inspection check.
2. Every type that lives in a Scene round-trips through JSON serialization. Add a round-trip test for each new serializable type.
3. Scene files start with `"formatVersion": N`. Loaders support previous versions or migrate them forward — never break old saves silently.
4. No statics, no singletons in the runtime. Everything is constructable in tests; the engine can run multiple instances in-process.
5. Determinism in core systems where feasible — fixed-step simulation tick, deterministic random with seedable state.

## Conventions

- File-scoped namespaces.
- One public type per file. Private helpers in the same file are fine.
- Tests mirror source layout: `tests/Engine.Core.Tests/Math/Vector2Tests.cs` matches `src/Engine.Core/Math/Vector2.cs`.
- Public API documented with XML doc comments.
- Internal/private by default; widen only on demand.
- Async only where it adds value (asset loading, file I/O). The game loop is sync.
- In hot paths (anything per-frame): prefer struct, prefer pooling, avoid LINQ, watch allocations. The engine targets steady-state zero managed-allocation update/render frames.
- See **Performance principles** in `PLAN.md` for the cross-cutting rules (bulk APIs, spatial hash, active radius, pooling). These bind every phase; phase deliverables that conflict with them get rewritten, not the principles.

## Build & test

```bash
dotnet build
dotnet test
dotnet run --project src/Engine.Editor
```

CI runs `dotnet build` + `dotnet test` on push and PR.

## Recommended Claude Code setup

- Model: `/model opusplan` — Opus plans each task, auto-switches to Sonnet for implementation. Best fit for this project's mix of architectural decisions and bulk implementation.
- Effort: `/effort high` as default. Bump to `xhigh` for the Phase 3 `AssemblyLoadContext` hot-reload spike specifically. Drop `ultrathink` into a single prompt when you need extra reasoning on one turn.

## Workflow notes for Claude Code sessions

- Before writing code, read `PLAN.md` and the current phase doc in `docs/`.
- Stay within the current phase's scope. Don't sneak in features from later phases — they'll be out of order and the code review will be harder.
- Write the test first when the spec is concrete. For UI work, manual verification is fine; describe what to click in the PR.
- Update the phase doc as tasks complete (check off boxes).
- If an invariant is violated or a test is skipped, say so explicitly in the response — never bury it.
- Prefer small, reviewable commits over large sweeping ones.

## Reading order for a fresh session

1. This file (`CLAUDE.md`)
2. `PLAN.md` — overall roadmap
3. `docs/phase-N.md` for the current phase
4. Relevant existing code in the area being touched
