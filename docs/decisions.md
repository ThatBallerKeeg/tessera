# Architecture Decisions

Running log of non-obvious choices made during implementation.

---

## Phase 1 — Task 1.1: MonoGame-in-Avalonia viewport (2026-05-17)

**Decision:** Option 1 (offscreen RenderTarget2D → WriteableBitmap) adopted with a
main-thread game loop, not a background thread.

**What we tried first:** Setting `SDL_VIDEODRIVER=offscreen` and running `Game.Run()` on a
background thread. `Run()` was called successfully but `Initialize()` was never reached —
SDL2's Cocoa back-end on macOS requires the main thread even for the offscreen driver (it
calls `[NSApplication sharedApplication]` during `SDL_Init`).

**What works:** Call `DoInitialize()` (internal MonoGame method, accessed via reflection)
from the Avalonia `OnLoaded` handler — which runs on the main thread — then drive the game
loop by calling `Game.Tick()` (public in MonoGame 3.8) from a `DispatcherTimer` at ~60 fps.
Both SDL event processing and OpenGL commands run on the main thread, satisfying macOS
Cocoa requirements.

**Trade-off:** MonoGame ticks share the Avalonia main thread. Each tick does a GPU→CPU
pixel readback (`RenderTarget2D.GetData`), which takes 1–5 ms at editor-preview resolutions.
At 60 fps this leaves ~11 ms for Avalonia UI. Acceptable for v0.1; if profiling shows
contention, promote to Option 2 (shared GL context via Silk.NET / Avalonia custom
`ICustomDrawOperation`).

**Files involved:**
- `Engine.Runtime/Viewport/ViewportGame.cs` — `StartManual()` + `TickPublic()` API
- `Engine.Editor/Viewport/MonoGameViewport.axaml.cs` — `DispatcherTimer`-driven loop

**MonoGame 3.8 specifics:**
- `Game.Tick()` is `public` — callable directly, no reflection needed.
- `Game.DoInitialize()` is `internal` — accessed via `BindingFlags.NonPublic` reflection.
- `Game.BeginRun()` is `protected` — accessed via `BindingFlags.NonPublic` reflection.
