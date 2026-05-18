# Phase 1 — Tilemap

Get tiles on screen. By end of phase you can paint multi-layered tilemaps in the editor, autotiling stitches edges across multiple terrains, water animates, and procgen can fill a 500×500 map in under 500ms.

This is the most algorithmically interesting phase of the early engine. Two parts will eat the most time: the **MonoGame-in-Avalonia viewport integration** (Task 1.1 — spike at the top of the phase, it's the biggest unknown) and **multi-terrain autotile resolution** (Task 1.4 — careful test design pays off, autotile bugs are very visible).

Refer back to PLAN.md's **Performance principles** throughout. Chunked + dirty-tracked + frustum-culled is non-negotiable for the renderer; bulk-edit API is non-negotiable for procgen.

## Tasks

### 1.1 MonoGame-in-Avalonia viewport (SPIKE FIRST)

This is the work deferred from Phase 0. No first-class library exists. Spike before anything else in this phase — if it doesn't work, the editor falls back to a separate viewport window and the rest of the phase needs UI rework.

Approaches in order of preference:

1. **Offscreen render target → WriteableBitmap.** Engine.Runtime renders to an offscreen `RenderTarget2D`, copy pixels to an Avalonia `WriteableBitmap`, display in an `Image` control. CPU↔GPU bounce per frame, but works cross-platform without special bindings. Performance ceiling around 60fps for modest scenes.
2. **Custom Avalonia control with shared GL context** via Silk.NET or OpenGL interop. Faster but platform-specific glue.
3. **Separate viewport window.** External child window managed by MonoGame, positioned over the placeholder panel. Janky but unblocks everything.

**Recommend option 1 for v0.1** — perf is good enough for editor preview, implementation is simplest. Promote to option 2 if perf becomes a real problem.

Deliverables:
- `Engine.Editor/Viewport/MonoGameViewport.axaml` + `.axaml.cs` — Avalonia control that hosts an EngineGame instance and displays its output.
- Replace the placeholder gray rectangle in MainWindow with this control in the Viewport panel.
- Mouse and keyboard events on the control forward to the EngineGame (will be consumed by tilemap painting in Task 1.6).
- Resizing the panel resizes the render target.

Acceptance:
- [ ] Editor opens; the Viewport panel shows MonoGame output (test by drawing a colored quad or grid).
- [ ] Resize the panel; render output resizes without crashing.
- [ ] Hover the panel; mouse position is forwarded and readable in user code.
- [ ] Sustained 60fps with a simple test scene (a tiled background).

If the spike fails, document the failure in `docs/decisions.md` (create the file if needed), drop to option 3, and proceed. Don't burn more than two days on option 1 before falling back.

### 1.2 Tilemap data model

Pure Engine.Core types, no MonoGame. The wire format is what gets saved to scenes.

Types in `Engine.Core/Tiles/`:

```csharp
public readonly struct TileId : IEquatable<TileId>
{
    public int Value { get; }       // 0 = empty by convention
    public static TileId Empty => new(0);
}

public sealed class TilesetData
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";          // relative to project root
    public Vector2Int TileSize { get; set; } = new(16, 16);  // per-tileset, NOT global
    public List<TileMetadata> Tiles { get; set; } = new();
}

public sealed class TileMetadata
{
    public TileId Id { get; set; }
    public bool Solid { get; set; }
    public float Friction { get; set; } = 1.0f;
    public string TerrainTag { get; set; } = "";         // e.g. "grass", "dirt", "water"
    public int TerrainPriority { get; set; }             // higher wins at conflicting edges
    public int BlobVariant { get; set; }                 // 0..46 for 47-blob (or -1 if not autotile)
    public List<TileFrame> Frames { get; set; } = new(); // empty list = static tile
}

public sealed class TileFrame
{
    public TileId TileId { get; set; }
    public int DurationMs { get; set; } = 200;
}

public sealed class TilemapData
{
    public string LayerName { get; set; } = "default";   // "floor", "walls", "decor", etc.
    public int LayerIndex { get; set; }                  // render order, ascending
    public Guid TilesetRef { get; set; }
    public Dictionary<ChunkCoord, ChunkData> Chunks { get; set; } = new();

    public IDisposable BeginBulkEdit();
    public void SetTile(Vector2Int worldPos, TileId tile);
    public TileId GetTile(Vector2Int worldPos);
    public event Action<ChunkCoord>? ChunkDirty;
}

public readonly struct ChunkCoord : IEquatable<ChunkCoord>
{
    public int X { get; }
    public int Y { get; }
    public const int ChunkSize = 16;
}

internal sealed class ChunkData
{
    public TileId[] Tiles { get; set; } = new TileId[16 * 16];
    public int NonEmptyCount { get; set; }   // auto-prune chunk when 0
}
```

**Bulk edit semantics:** while inside a `BeginBulkEdit()` scope, individual `SetTile` calls don't fire `ChunkDirty`. On disposal, it emits one event per affected chunk. This is the perf-critical part — procgen filling 500×500 tiles must touch chunk-dirty bookkeeping ~1024 times (once per chunk), not 250,000 times (once per tile).

**Layers:** each scene has zero or more TilemapData instances. Render order is determined by LayerIndex ascending. A typical RPG scene has floor (0), walls (1), decor (2).

**Serialization:** extend the Phase 0 SceneSerializer to handle TilemapData. Consider a custom `JsonConverter<ChunkData>` that base64-packs the tile array — TileId fits in 4 bytes, a chunk is 1024 bytes raw, base64 is tractable. Don't write 256 individual JSON ints per chunk.

Tests:
- Round-trip an empty TilemapData.
- Round-trip a TilemapData with ~50 tiles, verify all positions/IDs preserved.
- Bulk edit fires ChunkDirty once per affected chunk, not per tile (subscribe, write 1000 tiles across 8 chunks, assert exactly 8 events).
- Empty chunks get pruned: SetTile then erase, chunk dictionary shouldn't retain the empty chunk.

### 1.3 Procgen utilities

`Engine.Core/Procgen/`. Deterministic with explicit seeds. No hidden statics.

**`Random64.cs`** — xoshiro256** implementation. Reference: Blackman & Vigna's public-domain C is ~30 lines, trivially translatable. Supports:
- `ulong NextUInt64()`, `uint NextUInt32()`, `int NextInt32()`
- `double NextDouble()` and `float NextFloat()` — uniform [0, 1)
- `int NextInt(int minInclusive, int maxExclusive)`, `float NextFloat(float min, float max)`
- `Vector2 NextUnitVector()`, `Vector2 NextInsideUnitCircle()`
- `T Pick<T>(IReadOnlyList<T>)`, `void Shuffle<T>(IList<T>)` (Fisher-Yates)

Tests: first 100 outputs for seed `42` match a known reference sequence (hardcode them — this is how you catch bugs years later).

**`Noise2D.cs`**:
- `static float Perlin(float x, float y, int seed)` — classic Perlin, range roughly [-1, 1]
- `static float Simplex(float x, float y, int seed)` — Simplex, range roughly [-1, 1]
- `static float FBm(noiseFunc, x, y, seed, int octaves, float persistence = 0.5f, float lacunarity = 2.0f)` — fractal brownian motion

Tests: identical seed → identical output. Range within bounds across a sampled grid. Continuity (neighboring samples are close).

**`Poisson.cs`**:
- `static List<Vector2> Sample2D(float width, float height, float minDistance, Random64 rng, int maxAttempts = 30)` — Bridson's algorithm

Tests: every pair of points has distance ≥ minDistance; coverage density is in expected range.

**Zero allocations in inner loops** — noise is called millions of times during procgen. Use struct math; avoid LINQ.

### 1.4 Multi-terrain autotile

`Engine.Core/Tiles/Autotile.cs`. Pure algorithm, easily testable. **Get this right** — autotile bugs are extremely visible in-game.

**The 47-blob algorithm in one paragraph:** for each painted tile, look at its 8 neighbors. Each is either same-terrain or different. Build a bitmask where the 4 cardinals (N/E/S/W) are always counted, and each diagonal (NE/SE/SW/NW) is counted only if **both** of its adjacent cardinals are also same-terrain. This collapses 256 raw combinations to 47 valid corner-aware variants, which map 1:1 to a known tileset layout. Reference: search "47-tile blob autotiling" or "RPG Maker MV A1 tileset" for diagrams.

**Multi-terrain:** each terrain has a priority. When two terrains both want to draw at the same edge, the higher-priority terrain wins. So grass (priority 1) blobs over dirt (priority 0); water (priority 2) blobs over grass; at three-way corners, the highest-priority terrain dominates.

API:
```csharp
public static class Autotile
{
    /// <summary>
    /// Resolve which tile variant should appear at this position based on its neighbors.
    /// Returns TileId.Empty if the position itself is empty in the layer.
    /// </summary>
    public static TileId Resolve(
        TilemapData layer,
        Vector2Int position,
        TilesetData tileset);
}
```

**Where it's called:** the renderer calls `Resolve` every time a chunk rebuilds its render cache. The renderer never stores autotile-resolved IDs in the TilemapData — TilemapData holds the *intent* (terrain tag + base tile), the visual variant is computed at render time. This means edits don't fan out to neighbors.

Tests (these matter):
- All 47 neighbor configurations produce the documented variant index for a single-terrain map. Parametrize with `[Theory]` over a hardcoded list of (mask, expected_variant).
- Multi-terrain priority: paint grass-on-dirt; the dirt-side of every edge shows dirt's no-neighbor variant, the grass side shows grass's edge variant.
- Three-way corner: paint a cross of grass/dirt/stone meeting at one point; the highest-priority terrain wins the corner.
- Empty position returns `TileId.Empty` regardless of neighbors.

### 1.5 Tilemap renderer

`Engine.Runtime/Tiles/TilemapRenderer.cs`. Chunked + dirty-tracked + frustum-culled per Performance principles.

Deliverables:
- `TilemapRenderer` consumes TilemapData + TilesetData (textures loaded via MonoGame).
- Renders only chunks intersecting the camera AABB (frustum culling).
- Each chunk maintains a render cache (precomputed list of `(tile texture region, screen position)` quads). Cache invalidates when the chunk's tiles change (subscribe to `ChunkDirty`).
- Animated tiles share a global clock: a `TilemapClock` on the scene drives all animated tiles in sync. Frame index = `((clock.ElapsedMs / frame.DurationMs) mod frame_count)`.
- Multi-layer: renderer takes an ordered list of TilemapData, draws floor → walls → decor.

Tests (Engine.Runtime.Tests gets its first real tests here):
- Renderer with 0 chunks issues 0 draw calls.
- Renderer with 4 visible chunks out of 100 issues draw calls only for the 4 (use a mock SpriteBatch that counts calls).
- After `SetTile`, the affected chunk re-resolves; unaffected chunks don't (count cache rebuilds per chunk).
- Animated tile cycles correctly given a known clock state.

Manual benchmark: 60fps with a 100×100 visible area in a 1080p window. Log frame times every second to console for now (the F3 overlay is Phase 4).

### 1.6 Tilemap editor tools

`Engine.Editor/Tilemap/`. Hooks the viewport from Task 1.1.

Deliverables:
- New "Tilemap" tool panel docked next to Inspector. Contains:
  - Tileset selector (dropdown of loaded tilesets in the project)
  - Tile palette (scrollable grid of tiles in the selected tileset)
  - Selected-tile indicator
  - Tool buttons: Paint, Erase, Fill, Rect, Line, Picker
  - Active layer selector (dropdown of layers in the current scene)
  - Add Layer / Remove Layer / Move Up / Move Down
- Click-and-drag in the viewport → paint/erase/etc. with the selected tile.
- Fill tool uses flood fill bounded by terrain tag (don't flood across boundaries unless modifier held).
- Picker tool: click in the viewport → select the tile under the cursor in the palette.

Mouse-to-tile coords: viewport pixel position → world pixel → divided by tile size. Camera/zoom is a future concern; for v0.1 assume 1:1 with a fixed origin.

Acceptance: launch the editor, load a tileset, paint a 100×100 grass area by hand in under a minute. Save, reload, see it preserved.

### 1.7 Tileset import

Engine.Editor — File > Import Tileset.

User flow: pick a PNG. If a sibling JSON exists with the same basename (e.g. `grass.png` + `grass.tileset.json`), load metadata from it. Otherwise generate a default TilesetData with one auto-generated TileMetadata per slice of the PNG.

Sidecar JSON shape:
```json
{
  "tileSize": [16, 16],
  "tiles": [
    { "x": 0, "y": 0, "solid": false, "terrainTag": "grass", "terrainPriority": 1, "blobVariant": 0 }
  ]
}
```

Imported tilesets land in `<project>/Assets/Tilesets/<name>.tileset.json`. The editor maintains an asset browser panel listing them.

Tests: import a known PNG + sidecar, assert resulting TilesetData round-trips.

### 1.8 Procgen stress test (acceptance)

A dedicated test in Engine.Core.Tests that pins the performance acceptance. The shape:

```csharp
[Fact]
public void ProcGen_500x500_FromPerlin_BulkEdit_UnderHalfSecond()
{
    var tilemap = new TilemapData();
    var rng = new Random64(seed: 42);

    var sw = Stopwatch.StartNew();
    using (tilemap.BeginBulkEdit())
    {
        for (int y = 0; y < 500; y++)
        for (int x = 0; x < 500; x++)
        {
            var v = Noise2D.Perlin(x * 0.05f, y * 0.05f, seed: 42);
            tilemap.SetTile(new Vector2Int(x, y), v > 0 ? new TileId(1) : new TileId(2));
        }
    }
    sw.Stop();

    Assert.True(sw.ElapsedMilliseconds < 500,
        $"Bulk edit took {sw.ElapsedMilliseconds}ms, budget is 500ms");
}
```

Plus a naive-comparison test (writes WITHOUT BeginBulkEdit) to demonstrate the principle. The naive path should be at least 5× slower in CI; document the actual ratio in a comment. (PLAN.md's "100×" was aspirational — measure and pin the real number.)

Also: a determinism test that runs the same seed twice and asserts byte-identical tilemaps.

## Acceptance checklist

- [ ] MonoGame viewport renders inside Avalonia editor; resize works; mouse input forwarded.
- [ ] Tilemap data model serializes round-trip including bulk edits and chunked storage.
- [ ] xoshiro256** RNG passes reference-vector test (first 100 outputs match canonical seed 42 sequence).
- [ ] Perlin and Simplex noise produce identical output for identical seed across runs.
- [ ] Poisson-disk sampling respects min-distance for every returned pair.
- [ ] Single-terrain autotile produces correct variant for all 47 neighbor configurations.
- [ ] Multi-terrain autotile with three terrains resolves three-way corners by priority.
- [ ] Tilemap renderer draws only chunks intersecting camera AABB.
- [ ] Animated water tile (6 frames, 200ms each) cycles correctly, all instances in sync.
- [ ] Editor can paint a 100×100 multi-terrain map with autotile edges by hand.
- [ ] Save and reload preserves the map and its layers.
- [ ] 500×500 procgen + bulk edit completes in < 500ms.
- [ ] Naive (non-bulk) procgen is at least 5× slower in the same test.
- [ ] All tests green, architecture invariant holds (no MonoGame in Core).

## What's NOT in this phase

- Custom autotile rules beyond the 47-blob (full rule editor is Phase 6).
- Tilemap collision (Phase 4 reads the `Solid` flag).
- Lighting / shadows on tilemap (not planned for v0.1 — see PLAN.md limits).
- World streaming (bounded worlds only).
- Procgen UI inside the editor — procgen runs in user code; the engine ships the utilities.
- Camera zoom / pan in the editor viewport. v0.1 is 1:1 fixed origin.

## Suggested commit order

1. MonoGame-in-Avalonia spike (Task 1.1) — biggest unknown, do first
2. Tilemap data model + serialization (Task 1.2)
3. xoshiro256** RNG (part of 1.3)
4. Perlin + Simplex noise (part of 1.3)
5. Poisson-disk sampling (part of 1.3)
6. Single-terrain autotile (the 47-blob math, part of 1.4)
7. Multi-terrain priority resolution (rest of 1.4)
8. Tilemap renderer without animation (part of 1.5)
9. Animated tile support (rest of 1.5)
10. Tilemap tool panel UI (part of 1.6)
11. Editor painting integration (rest of 1.6)
12. Tileset import (Task 1.7)
13. Procgen stress test (Task 1.8)

Each is its own commit. Phase 1 is roughly **2-3 weeks** for someone working evenings, faster if full-time. Tasks 1.1 and 1.4 are where surprises live; the rest is execution.
