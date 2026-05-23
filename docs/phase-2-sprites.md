# Phase 2 — Sprites & Animation

Bring entities to life. By end of phase you can import a Mystic Woods spritesheet from Aseprite-exported JSON, attach a SpriteRenderer + Animator to a GameObject, drop it in a scene, and watch it walk through four-direction animation cycles with event markers firing on footfall frames.

Two parts will eat the most time: the **runtime entity/component foundation** (Task 2.1 — first time the engine actually ticks user objects per frame, and the bedrock everything in Phase 3+ stands on) and the **animation editor panel** (Task 2.5 — timeline scrubber, live preview, event markers visualized).

**Aseprite JSON import (Task 2.6) is the highest-leverage adoption feature per PLAN.md.** Aseprite is the standard pixel-art tool; supporting its JSON export means you can hand artists "use Aseprite, export to JSON, drag into editor" instead of explaining a bespoke pipeline.

## Tasks

### 2.1 Runtime entity/component foundation

Engine.Runtime gets a real component system. Until now Task 0.6's `LoadScene` created internal placeholders for GameObjects but they didn't tick or render. Phase 2 changes that — sprites need live entities to attach to. This task is foundational for everything in Phase 3+, so it's bigger than it looks.

Deliverables in `Engine.Runtime/Entities/`:

- `Component` abstract base class: virtual `OnAttach(GameObject)`, `OnUpdate(float deltaSeconds)`, `OnDraw(ISpriteBatch)`, `OnDetach()`. Subclasses override what they need.
- `GameObject` runtime class: `Vector2 Position`, `Guid Id`, `string Name`, `IReadOnlyList<Component> Components`, plus `AddComponent<T>()` / `GetComponent<T>()` / `RemoveComponent<T>()`. No scene graph yet — flat list is fine for v0.1.
- `Scene` runtime container: ordered tilemap layers from Phase 1 plus the live GameObject list (one per GameObjectData from SceneData).
- `SceneLoader`: instantiates a live Scene from a SceneData. For Phase 2, component instantiation from ComponentData is a hardcoded switch keyed on TypeName (e.g. `"SpriteRenderer"` → `new SpriteRenderer()`, populated from the Fields dict). Phase 3 swaps this for reflection-based instantiation of user-scripted components — but the seam is here.
- `EngineGame.Update(GameTime)` ticks the scene: for each GameObject, for each Component, call `OnUpdate(deltaSeconds)`. Wall-clock delta in seconds.
- `EngineGame.Draw` renders the scene: tilemaps first (Phase 1 path), then GameObjects sorted by Y position for fake depth (lower Y = drawn first = behind), then GameObjects' `OnDraw(ISpriteBatch)`.

Tests in `Engine.Runtime.Tests`:
- A test Component that increments a counter in `OnUpdate` — confirm called once per Update with correct delta.
- Multiple Components on one GameObject all tick.
- GameObjects sort by Y position when drawn (use the counting ISpriteBatch from Phase 1.5).
- ComponentData → live Component round-trip via SceneLoader for the engine-builtin types.

User-scripted components don't appear until Phase 3. For Phase 2, Components are engine-builtin only (`SpriteRenderer`, `Animator`, plus the test ones).

### 2.2 Sprite + Spritesheet data model

Engine.Core/Sprites/. Pure data, no MonoGame.

```csharp
public readonly struct SpriteId : IEquatable<SpriteId>
{
    public int Value { get; }
    public static SpriteId Empty => new(0);
}

public sealed class SpritesheetData
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public List<SpriteFrame> Frames { get; set; } = new();  // explicit, not grid
}

public sealed class SpriteFrame
{
    public SpriteId Id { get; set; }
    public string Name { get; set; } = "";  // optional; useful for Aseprite frame names
    public Rectangle SourceRect { get; set; }  // pixels in the PNG
    public Vector2 Pivot { get; set; }  // origin in pixels (default = top-left)
}
```

**Why explicit frames rather than grid slicing?** Aseprite-exported spritesheets are usually packed (no guarantee frames align to a grid), and the JSON tells you exactly where each frame is. A grid helper for ad-hoc imports is still useful — add `SpritesheetData.GenerateGrid(name, imagePath, imageSize, frameSize)` as a convenience for non-Aseprite imports.

Serialization extends `SceneSerializer` (the converter pattern from Phase 0/1 is established).

Tests: round-trip empty, round-trip with multiple frames, `GenerateGrid` produces expected frame count and source rects.

### 2.3 AnimationClip + AnimationEvent

Engine.Core/Animation/. Pure data.

```csharp
public sealed class AnimationClip
{
    public string Name { get; set; } = "";  // matches Aseprite tag names (e.g. "walk_north")
    public List<AnimationClipFrame> Frames { get; set; } = new();
    public bool Loops { get; set; } = true;
    public List<AnimationEvent> Events { get; set; } = new();
}

public sealed class AnimationClipFrame
{
    public SpriteId SpriteId { get; set; }
    public int DurationMs { get; set; } = 100;
}

public sealed class AnimationEvent
{
    public string Name { get; set; } = "";  // e.g. "footstep_left", "attack_hit"
    public int FrameIndex { get; set; }     // 0-based, fires when this frame becomes active
}
```

`Loops = false` is how one-shot sprites work for particles (per PLAN.md's particle decision) — the Animator marks itself complete; an `AutoDestroy` component watches for that and removes the GameObject. Defer AutoDestroy itself to Phase 5 unless needed sooner.

Tests: serialization round-trip; event markers serialize with correct frame indices.

### 2.4 SpriteRenderer + Animator components

`Engine.Runtime/Components/`. Both extend the `Component` base from Task 2.1.

**`SpriteRenderer`:**
- Holds a `SpritesheetData` reference and a `SpriteId CurrentFrame` to display.
- In `OnDraw(ISpriteBatch)`, looks up the source rect for `CurrentFrame` in the spritesheet and draws at the GameObject's Position, honoring pivot offset.
- Texture is loaded via the same `TextureCache` introduced in Phase 1.5.

**`Animator`:**
- Holds the current `AnimationClip` (settable via `Play(clip)` from user code).
- Tracks elapsed time within the clip.
- In `OnUpdate`, advances elapsed by `deltaSeconds * 1000`, computes the current frame index from the cumulative duration table, fires any `AnimationEvent` whose `FrameIndex` was just crossed.
- Exposes `CurrentSpriteId` for sibling SpriteRenderer to read.
- `IsComplete` returns true for non-looping clips at their final frame.
- `Play(clip)` resets elapsed to 0 and switches clips.

**Wiring between them:** `SpriteRenderer.OnDraw` queries the sibling `Animator.CurrentSpriteId` (via `GameObject.GetComponent<Animator>()`) each frame and uses that as the source. If no Animator is attached, SpriteRenderer just uses its own `CurrentFrame` field — supports both animated and static use.

Tests:
- Animator advances time and updates CurrentSpriteId at correct frame durations.
- Looping clip wraps to frame 0 after the last frame's duration elapses.
- Non-looping clip stops at the final frame and marks `IsComplete = true`.
- AnimationEvents fire exactly once when their frame becomes current; multiple events at one frame all fire.
- `Play(clip)` resets elapsed time and switches frame to clip's frame 0.
- SpriteRenderer + Animator: with both attached, SpriteRenderer draws Animator's current frame, not its own field.

### 2.5 Animation editor panel

`Engine.Editor/Animation/`. The most UI-heavy task in Phase 2.

Deliverables:
- New "Animation" panel, dockable like Tilemap (suggest: same column as Tilemap, switched via tabs, since both are tile/sprite-authoring tools and the user only uses one at a time).
- **AnimationClip browser** at top: lists clips in the active Spritesheet asset; click to select.
- **Frame list view:** displays frames in the selected clip with their durations and a thumbnail of each.
- **Timeline scrubber:** drag a playhead through the clip; live preview updates as you scrub. Event markers visualized as small flags at their frame positions.
- **Live preview area:** the current frame's sprite at native size, with a zoom control to scale it up (4× default — same problem you hit in Phase 1's viewport).
- **Transport controls:** Play, Pause, Stop, frame-step forward/back.
- **Editing operations:** change frame duration (numeric input), reorder frames (drag), add/remove frames, add/remove event markers (click on timeline at a frame, name it).

Implementation note: the live preview is the runtime SpriteRenderer + Animator from Task 2.4, embedded in a small offscreen render target. Reuse the runtime components instead of re-implementing playback for the editor — same principle as Phase 1's viewport hosting the same runtime renderer.

Acceptance (manual): import a Mystic Woods walk cycle, see all four directional clips in the browser, select walk_south, scrub through it, watch the preview update, add a "footstep_left" event marker at frame 2, see it appear on the timeline.

### 2.6 Aseprite JSON import

Engine.Editor — File > Import Spritesheet (Aseprite JSON).

Aseprite's "Hash" JSON export shape:
```json
{
  "frames": {
    "player 0.png": { "frame": {"x": 0, "y": 0, "w": 16, "h": 16}, "duration": 100 },
    "player 1.png": { "frame": {"x": 16, "y": 0, "w": 16, "h": 16}, "duration": 100 }
  },
  "meta": {
    "image": "player.png",
    "size": {"w": 64, "h": 64},
    "frameTags": [
      {"name": "walk_north", "from": 0, "to": 3, "direction": "forward"},
      {"name": "walk_south", "from": 4, "to": 7, "direction": "forward"}
    ]
  }
}
```

Each frame becomes a `SpriteFrame`. Each `frameTag` becomes an `AnimationClip` referencing frames `from..to`. The image path comes from `meta.image` (resolved relative to the JSON file).

User flow: File > Import Spritesheet → pick the JSON file → importer creates one SpritesheetData and one AnimationClip per tag → saved to `<project>/Assets/Sprites/<name>.spritesheet.json` plus `<project>/Assets/Animations/<spritesheet>_<tag>.clip.json` per tag.

**Extend the Assets browser from Phase 1.7** to list both Tilesets and Spritesheets. Tilesets get a tile icon; Spritesheets get a sprite icon. Clicking either type activates it for the relevant tool panel (Tilemap or Animation).

Tests in Engine.Core.Tests/Sprites/: parse a known Aseprite JSON string, assert resulting SpritesheetData and AnimationClips have the expected frame counts, source rects, durations, and tag names.

### 2.7 Acceptance demo

End-to-end test in the editor proving everything works together:

1. Export a Mystic Woods spritesheet from Aseprite as Hash JSON (or hand-write a JSON matching the format if you don't have the source `.aseprite`).
2. Import it via File > Import Spritesheet — lands in the Assets browser.
3. Add a new GameObject to the current scene (need a "+" button on the Scene panel for this — small UI addition).
4. Attach a SpriteRenderer + Animator via a temporary "Add Component" dropdown listing engine-builtin component types (Phase 3 replaces this dropdown with a reflection-driven version listing user-scripted components too).
5. Set the Animator's clip to `walk_south`.
6. Click Play in the editor's runtime preview.
7. The player sprite cycles through walk_south frames at the imported durations.
8. Add a "footstep_left" event marker at frame 2. Confirm it fires every loop (log to console via a stub handler).

## Acceptance checklist

- [ ] Runtime entity/component foundation: GameObjects with multiple Components tick per frame, OnDraw called in Y-sorted order.
- [ ] Spritesheet data model round-trips through SceneSerializer.
- [ ] AnimationClip data model round-trips, including AnimationEvent frame indices.
- [ ] SpriteRenderer draws the correct source rect from the spritesheet at the GameObject's position.
- [ ] Animator advances frames according to per-frame durations.
- [ ] Looping clip wraps to frame 0; non-looping clip stops at the final frame and marks IsComplete.
- [ ] AnimationEvents fire exactly once per loop on the correct frame.
- [ ] Animation editor timeline scrub updates live preview correctly.
- [ ] Event markers visible on the timeline at their FrameIndex positions.
- [ ] Aseprite JSON import produces SpritesheetData + AnimationClips matching the export shape.
- [ ] Mystic Woods player walks on screen with synchronized animation in the editor preview.
- [ ] All tests green, architecture invariants hold (Engine.Core has no MonoGame/Avalonia references).

## What's NOT in this phase

- Animation state machines (transition graphs / node editor). Animator switches clips by code or by editor selection only.
- Skeletal animation, bones, IK. Pixel art doesn't need it.
- Procedural animation (squash-and-stretch, breathing, etc.).
- A full particle system (emitters, lifetime curves, velocity, gravity). One-shot non-looping sprites cover footstep dust and similar; full particle system stays post-0.1 per PLAN.md's decision.
- Sprite atlas packing — taking N small PNGs and packing into one atlas. Aseprite exports already-packed sheets.
- Animation blending / cross-fade. Sharp cuts only.
- User-scripted components — that's Phase 3.

## Suggested commit order

1. Runtime entity/component foundation (Task 2.1) — biggest, foundational, do first.
2. Sprite + Spritesheet data model (Task 2.2).
3. AnimationClip + AnimationEvent data model (Task 2.3).
4. SpriteRenderer component (part of 2.4).
5. Animator component (rest of 2.4).
6. Animation editor panel scaffold + frame list + timeline (part of 2.5).
7. Live preview wiring + event marker visualization + transport controls (rest of 2.5).
8. Aseprite JSON parser + import flow (Task 2.6).
9. Assets browser extension for multiple asset kinds.
10. "Add Component" dropdown + acceptance demo (Task 2.7).

Each is its own commit. Phase 2 is roughly **2-3 weeks** at side-project pace, faster full-time. Tasks 2.1 and 2.5 are the heaviest; the rest is execution. The acceptance demo at 2.7 is the satisfying payoff — first time the engine renders a moving character.
