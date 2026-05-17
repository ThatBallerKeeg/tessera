# Engine Roadmap

A 2D pixel-art engine for top-down RPGs, roguelikes, and sandbox games. Real-time movement, C# scripting, support for procedurally-generated bounded worlds, public release.

Realistic timeline to a usable 0.1 public release: **4-6 months** as a side project, faster if full-time.

Each phase has an acceptance checklist. Don't move on until everything is checked.

---

## Performance principles

This is a real-time engine targeting sandbox-scale games (thousands of entities, hundred-thousand-tile worlds, persistent simulation). These principles run through every phase — if a phase deliverable conflicts with one, the deliverable changes.

- **Chunked, dirty-tracked tilemap.** Tilemap is sparse 16×16 chunks. Only dirty chunks redraw; only tiles whose neighbors changed re-evaluate their autotile rule.
- **Bulk APIs for procgen.** Setting 100,000 tiles during world generation must not fire 100,000 individual change events. Provide `SetTiles(batch)` / `using (tilemap.BeginBulkEdit())` that defers dirty-chunk notifications until the batch ends.
- **Spatial hash for entity queries.** All "what's near me" lookups (collision broad-phase, AI perception, pickup detection) go through a uniform-grid spatial index updated per-tick. Never iterate all entities for a proximity check.
- **Active radius / entity sleep.** Entities outside the player's active radius pause `Update` calls or run on a slower tick (e.g. 1 Hz). Distant mobs do not burn CPU. Configurable per-component (some entities — quest givers, save points — always tick).
- **View frustum culling.** Renderer iterates only entities and chunks intersecting the camera AABB.
- **Pooling for high-churn objects.** Projectiles, transient sprite-particles, dropped items, damage numbers — all from pools, never `new`'d per-frame.
- **Zero allocations in hot paths.** `Update` / `Render` / collision code paths allocate nothing under steady state. Verify with an allocation profiler periodically; add an automated allocation-budget test in CI for the demo project.
- **Deterministic where feasible.** Fixed-step simulation tick; seedable deterministic RNG (xoshiro256** or PCG, implemented in `Engine.Core`); prefer `float` over `double` for game state.

When a Phase deliverable lists "spatial hash" or "pooling" or similar, it's referring back to this section.

---

## Phase 0 — Foundation (1-2 weeks)

**Goal:** End-to-end skeleton. Solution builds, tests run, editor opens, empty scene saves and loads.

Deliverables:
- Solution with three projects + tests projects.
- CI builds and tests on push.
- Architecture invariant check (Engine.Core has no MonoGame/Avalonia references).
- `Engine.Core`: Vector2 / Vector2Int / Rectangle math types. Scene + GameObject + Component data classes (no behavior yet). System.Text.Json serializer with version field.
- `Engine.Runtime`: MonoGame `Game` class that opens a window and clears to color. `LoadScene(SceneData)` consumes Core data (renders nothing yet).
- `Engine.Editor`: Avalonia window with placeholder docked panels. File > New / Open / Save Scene exercises serialization.
- Round-trip serialization test for an empty scene and a scene with one GameObject + one component.

Acceptance:
- [ ] `dotnet build` clean.
- [ ] `dotnet test` green.
- [ ] Editor opens, can save an empty scene and load it back.
- [ ] CI passes on a clean clone.
- [ ] Invariant check fails the build if MonoGame/Avalonia is added to Engine.Core.

Detailed task spec: [`docs/phase-0-foundation.md`](docs/phase-0-foundation.md)

---

## Phase 1 — Tilemap (2-3 weeks)

**Goal:** Paint a tilemap in the editor; the runtime renders it efficiently; autotiling stitches edges automatically.

Deliverables:
- Tileset asset: image reference + per-tile metadata (solid, friction, terrain tag, terrain priority for layering).
- Tilemap data structure (chunked, sparse-friendly — 16×16 chunks in a dictionary keyed by chunk coord). Supports **bulk-edit mode**: `using (tilemap.BeginBulkEdit()) { ... }` defers dirty notifications and autotile recomputation until disposal. Required for procgen — naive per-tile writes during world gen would be catastrophic.
- Engine.Runtime: chunked tilemap renderer, only redraws dirty chunks. View frustum culling — chunks outside the camera AABB are skipped entirely.
- Editor: tilemap tool panel — paint, erase, fill, rect, line, picker. Multi-layer support (e.g. floor, walls, decor).
- **Autotiling — multi-terrain.** Start with the 47-tile blob scheme but support **multiple terrain layers transitioning onto each other** (grass blobs onto dirt, dirt blobs onto stone, water blobs onto sand). Each tileset tile declares its terrain tag + which terrains it transitions over, and the runtime resolves edges via terrain priority. This is the minimum needed for procedurally-generated biomes; without it, procgen looks like a coloring book. A fully-generic custom rule editor still comes in Phase 6 — this phase covers the multi-terrain blob, which is sufficient for most procgen.
- **Animated tiles.** A tile in the tileset can carry a list of frames + per-frame duration. The renderer drives them off a global animation clock so all instances of an animated tile stay in sync. Required for water, lava, sparkles in tilesets, etc.
- **Procgen support utilities (in `Engine.Core`):** seedable deterministic RNG (xoshiro256** or PCG), 2D Perlin and Simplex noise, Poisson-disk sampling for feature placement. These are not part of the tilemap itself but ship with Core because every procgen user needs them and rolling your own is error-prone.
- Tileset import: PNG + JSON sidecar describing tile metadata.
- **Decision needed early:** MonoGame-in-Avalonia viewport integration. Spike this at the top of the phase. There's prior art but no first-class library — budget research time.

Acceptance:
- [ ] Paint a 100×100 grass-with-water-edges map; autotiles produce correct edge tiles for all 47 neighbor configurations.
- [ ] Multi-terrain transitions work: a map of grass / dirt / stone shows correct edges between each pair, with priority resolving three-way corners.
- [ ] An animated water tile (6 frames, 200ms each) cycles correctly when placed, and all instances stay in sync.
- [ ] Editor remains responsive when painting on a 500×500 map.
- [ ] Runtime renders at 60fps for a 1080p viewport showing a 100×100 area.
- [ ] **Procgen stress test:** bulk-edit a 500×500 tilemap from noise in a single `BeginBulkEdit` block in under 500ms on a mid-tier dev machine. Naive per-tile writes (the failure mode) should be at least 100× slower for comparison in the test output.
- [ ] Deterministic RNG produces identical sequences across runs for the same seed.

---

## Phase 2 — Sprites & animation (1-2 weeks)

**Goal:** Define spritesheets, build animation clips, preview them in-editor, play them at runtime.

Deliverables:
- Sprite / Spritesheet assets with frame slicing.
- AnimationClip: ordered frames with per-frame duration and named event markers.
- Animator component — single clip + flag-based switching for v1. No full state-machine UI yet (defer to v0.2 if needed).
- Editor: animation panel with timeline scrubber, frame inspector, live preview.
- **Aseprite import** (`.aseprite` binary or exported PNG+JSON). Highest-leverage feature you can add here — Aseprite is the standard pixel-art tool and supporting it makes the engine instantly attractive.

**Decision: particles.** For v0.1, particles are treated as one-shot animated sprites — fire-and-forget GameObjects that play an animation clip and destroy themselves. This is free with this phase and is sufficient for dust puffs, footstep effects, hit flashes, simple sparkles. A proper particle system (emitter components, lifetime curves, velocity/gravity, burst vs continuous) is **deferred to post-0.1** unless a specific game-design need forces it earlier. Revisit when shipping the sample project — if dust-as-sprite feels insufficient, promote particles to a real phase.

Acceptance:
- [ ] Import a 4-direction walk cycle from Aseprite; play it on a GameObject in the runtime.
- [ ] Event markers fire callbacks at the right frame.
- [ ] Timeline scrubbing in the editor updates the preview live.

---

## Phase 3 — Scripting & inspector (3-4 weeks, hardest phase)

**Goal:** Users write `Component` subclasses in C#, save the file, see them reload live in the editor with serialized fields editable in an inspector.

Deliverables:
- Roslyn-based compilation of a user's `.csproj`.
- **Collectible `AssemblyLoadContext` for hot-reload.** Spike this first as a standalone experiment — confirm collectibility actually works for your component instantiation pattern before building UI on top. Subscribed events and static state in user code can pin assemblies; document the rules clearly.
- File watcher: recompile on script save, reload assembly, re-instantiate components preserving serialized state.
- Reflection-based inspector: walks public fields + `[Serialize]`-attributed private fields, renders Avalonia controls per type (int → NumericUpDown, Vector2 → two inputs, asset ref → drag target, enum → dropdown, etc.).
- Prefab system: an entity template you can instance into scenes; instances track per-instance diffs from the prefab.
- Scene editor: combined tilemap + entity placement on the same MonoGame viewport.
- **Spatial hash for entity queries.** Uniform-grid index over entity positions, updated per-tick. All `OverlapCircle`, `OverlapRect`, `Raycast`, and "find entities near X" queries route through it. Cell size configurable per-scene (default ~2× the largest common entity). This is mandatory before Phase 4's collision; n² broad-phase will not survive sandbox entity counts.

Risks and mitigations:
- ALC collectibility is finicky. **Fallback:** full editor-state restart on script change (slower but unblocks Phase 3 if collectibility proves unreliable). Decide at the end of the spike.

Acceptance:
- [ ] Add a `public int health = 10` field to a Component, save the file, see it appear in the inspector without restarting the editor.
- [ ] Place 20 entities in a scene, save, close, reopen — identical state.
- [ ] Prefab change propagates to all instances except per-instance overrides.
- [ ] Spatial hash returns correct neighbors for a scene with 2,000 entities; an `OverlapCircle` call costs < 0.1ms.

---

## Phase 4 — Gameplay essentials (2-3 weeks)

**Goal:** Make the engine playable for real — collision, camera, input, audio.

Deliverables:
- AABB collision: entity-vs-tilemap (using the solid flag from Phase 1), entity-vs-entity (broad-phase via Phase 3's spatial hash, then narrow-phase AABB), triggers (overlap-only, no response).
- Camera component: smooth follow with deadzone, pixel-snap option.
- Input action system: named actions, multiple bindings per action, keyboard + gamepad, runtime rebinding.
- Audio: SFX and music via MonoGame audio. Volume buses (master / sfx / music).
- **Object pooling helpers.** Generic `Pool<T>` plus a `PoolableComponent` base that handles get/return through the scene. Document the contract: pooled components must implement `OnGet` and `OnReturn` to reset state. Reference pool sizes for typical sandbox use: projectiles 256, sprite-particles 512, dropped items 128, damage numbers 64.
- **Active-radius update system.** Each Component declares an `UpdateMode`: `Always`, `WhenActive` (default — only ticks when within the player's active radius), or `SlowTick` (ticks at a configurable lower rate, e.g. 1 Hz, regardless of distance). Scene-level setting controls radius. Distant mobs in a sandbox should be `SlowTick` so they wander/respawn but don't burn frame time.
- **Frame budget profiling overlay.** Toggleable in-editor + in-game F3 overlay: frame time, GC allocations this frame, entity counts (total / active-ticked / slow-ticked / sleeping), spatial-hash query count, draw call count, dirty chunk count. Make slow things visible.

Acceptance:
- [ ] Walk a player around a tilemap with collision against walls.
- [ ] Camera follows smoothly with deadzone; pixel-snap toggle works.
- [ ] Gamepad and keyboard both control the player without code changes — only binding config differs.
- [ ] Sandbox stress scene: 200×200 tilemap, 1,500 entities (mix of items, mobs, particles), player walking — holds 60fps. Verified via the frame-budget overlay.
- [ ] With active-radius enabled, only ~50-100 entities tick per frame in that scene; the rest are slow-ticking or sleeping.
- [ ] Steady-state frame allocates < 1 KB managed memory (excluding the first few warmup frames).

---

## Phase 5 — RPG systems (3-4 weeks)

**Goal:** Build the systems an RPG/roguelike actually needs.

Deliverables:
- Dialog editor — Avalonia node graph — plus runtime dialog player with branching, conditions, callbacks into user scripts.
- A* pathfinding on the tile grid for AI.
- Trigger volumes / region components.
- Inventory primitives: Item asset type, Inventory component, stackable / unique flags. Users build their own UI on top.
- Save / load system using your serializer; full game state round-trip including dynamic entities.

Acceptance:
- [ ] Talk to an NPC, walk through a branching dialog, see effects on the world (flag set, item given).
- [ ] An enemy paths around obstacles to reach the player.
- [ ] Save game, close editor, reopen, load — exact state restored.

---

## Phase 6 — Public-release polish (ongoing)

**Goal:** Ship 0.1 publicly. Make it adoptable.

Deliverables:
- Tiled `.tmx` import (so users with existing maps can migrate in).
- One-click build: editor produces a zipped redistributable game for Win/Mac/Linux.
- Documentation site (DocFX or VitePress) — install, first project, every tool, the scripting API.
- Sample project shipped with the engine — non-trivial, like a small dungeon explorer. This matters enormously for adoption.
- Custom autotile rule editor (generalizes Phase 1's blob).
- Scene format versioning and migrations validated against a corpus of old saves.
- Plugin / extension API (let users add editor tools).

Acceptance:
- [ ] Fresh user can clone the engine, open the sample project, build it, run the output — all without manual setup.
- [ ] Documentation covers install, first project, every tool, the scripting API.

---

## Living document

Update this file as scope shifts. Add a "Decisions" section per phase to record architecture calls made during implementation, especially anything that contradicts the original spec — future you and future Claude Code sessions will thank you.
