# CLAUDE.md — DuckAround (internal: UntitledDuckGame)

Guidance for Claude / AI agents working in this Unity project. Read this first, then the
detailed docs under [.claude/docs/](.claude/docs/).

> **Tentative title:** *DuckAround* · **Internal/repo name:** UntitledDuckGame
> **Engine:** Unity **6000.0.71f1** (Unity 6) · **Render pipeline:** URP 17.0.4
> **Target:** PC desktop, **mouse + keyboard** only · **Audio:** FMOD

---

## 0. Working rules (read before editing anything)

1. **Above all else: do NOT edit files you have not been explicitly authorized to edit.**
   If a change seems to require touching a file outside the request, stop and ask first.
2. **Ask before assuming.** This project is intentionally complex and much of the design
   intent is *not* in the code. When intent is unclear, ask a clarifying question rather
   than guessing. (This doc and the `.claude/docs/` set were built that way.)
3. **Do not touch third-party assets or binary scenes/prefabs unless asked.** Specifically
   avoid editing under `Assets/AmplifyShaderEditor/`, `Assets/Plugins/`, `Assets/ConsolePro/`,
   `Assets/TextMesh Pro/`, and the `.unity` / `.prefab` files (they're huge YAML blobs that
   are easy to corrupt and hard to review). Edit C# scripts and ask before anything else.
4. **Hot Reload is installed and active** (Singularity Group Hot Reload). **Method-body edits**
   apply live (no recompile). But **new files, new methods, or changed method signatures require a
   FULL recompile** — Hot Reload won't pick them up with a live patch, so after such a change tell
   the user to recompile (focus Unity / Hot Reload "Recompile" / Reimport).
   ⚠️ Trap: when a change spans multiple edits (e.g. a signature + its call sites), Hot Reload can
   compile the **half-applied intermediate state**, hit a transient error, then stay stuck on that
   stale error even after the final edit is correct. Land signature + call-site changes together to
   minimize the broken window, and recompile fully afterward.
   **Remote trigger:** at the end of a batch of edits, queue the right action by writing to the
   trigger file the editor watches. Refined rule: `echo recompile > Library/hotReloadRemoteRequest.txt`
   **only when a brand-new function is created and then called** (or a new used script); for everything
   else — body edits, field add/change, signature tweaks — `echo reload > …`. See
   [.claude/docs/hot-reload-remote.md](.claude/docs/hot-reload-remote.md) for the rubric and how it
   works ([Assets/Editor/HotReloadRemote.cs](Assets/Editor/HotReloadRemote.cs)).
5. **Match the existing C# style** — governed by [.editorconfig](.editorconfig): **tabs**
   (width 4), `csharp_new_line_before_open_brace = none` (opening brace on the same line),
   `using` directives sorted system-first, UTF-8, LF line endings, final newline.
6. **Commit messages**: short title line, then a few SHORT bullet points (a couple words
   each, terse phrases — NOT sentences or paragraphs). Match the existing `git log` style.
   Never a title-only commit, never a verbose paragraph body.

---

## 1. What the game is

A 3D **isometric sabotage** game. You play **QuackBot**, a duck-shaped factory robot. You
start restrained, **break free** by mashing WASD, then roam a factory using a mouse-driven
**articulated arm** (inverse kinematics) to grab, shove, and (eventually) throw objects and
operate machinery.

The antagonists are **Workers** — helmeted robots that **patrol** a waypoint graph, **detect**
you through a vision cone, and **chase** you. Hitting/shoving a worker **ragdolls** it. A
global **Alarm** (levels L0–L7) represents how alert the workers are: higher alarm = wider/
longer vision and faster chasing. The intended loop is to **disrupt production and knock out
workers** while managing the alarm (planned: reset it via levers).

See [.claude/docs/game-overview.md](.claude/docs/game-overview.md) for the full concept and
[.claude/docs/status-roadmap.md](.claude/docs/status-roadmap.md) for **what works vs. what's
planned/buggy** (important — several described mechanics are not wired up yet).

---

## 2. Repo / asset map

```
UntitledDuckGame/                 ← Unity project root (this folder; contains Assets/)
├─ Assets/
│  ├─ Scripts/                    ← ALL gameplay code (the part you'll mostly work in)
│  │  ├─ AI/                      ← Worker AI: behaviour tree, waypoints, detection, ragdoll
│  │  ├─ Audio/                   ← FMOD wrappers (AudioManager / AudioAgent / FMODEvents)
│  │  ├─ Conveyors/               ← Conveyor path/belt system
│  │  ├─ Debug/                   ← Dev/test helpers (spawners, trigger volumes, reporters)
│  │  ├─ GlobalState/             ← GlobalAlarm + AlarmManager (the alarm singleton + HUD)
│  │  ├─ Interactables/           ← LeverInteractable, PickupInteractable (IInteractable impls)
│  │  ├─ Items/                   ← Cube/fan/prop behaviours & spawners
│  │  ├─ Player Scripts/          ← PlayerDuckController, IsometricRaycaster (arm/IK), etc.
│  │  └─ UI/                      ← CursorController (custom cursor + click-to-interact)
│  ├─ Editor/                     ← Custom editor windows/tools (Tools/ menu)
│  ├─ Scriptable Objects/         ← AlarmConfig (AlarmLevelData.cs) + AlarmLevelDataStandard.asset
│  ├─ Scenes/                     ← Demo.unity (MAIN), WorkerAlarmDemo, WorkerBTDemo (testbeds)
│  ├─ Prefabs/, Models/, Animations/, Materials/, Textures/, Shaders/, VFX/, Audio/
│  ├─ StreamingAssets/            ← FMOD banks
│  ├─ DebugCameraMover.cs, DebugCanvasManager.cs, OuterCircleSpriteManager.cs  ← loose root scripts
│  └─ (third-party: AmplifyShaderEditor/, Plugins/, ConsolePro/, TextMesh Pro/)
├─ Packages/manifest.json         ← package dependencies
├─ ProjectSettings/               ← TagManager (layers/tags), ProjectVersion, etc.
└─ .claude/docs/                  ← detailed documentation (see index below)
```

**Main scene:** [Assets/Scenes/Demo.unity](Assets/Scenes/Demo.unity). The other two scenes are
feature testbeds (alarm tuning, behaviour-tree iteration).

---

## 3. Systems at a glance

| System | Entry point(s) | Doc |
| --- | --- | --- |
| Player movement + IK arm + interaction + carry/fling | `PlayerDuckController`, `IsometricRaycaster`, `HeldItemController`, `CursorController` | [player.md](.claude/docs/player.md) |
| Worker AI (BT, patrol, detect, chase, ragdoll) | `WorkerAIController`, `PrimaryBTBuilder`, `Pathfinding` | [worker-ai.md](.claude/docs/worker-ai.md) |
| Global alarm (alert level L0–L7) | `GlobalAlarm`, `AlarmConfig`, `AlarmManager` | [alarm.md](.claude/docs/alarm.md) |
| Interactables & items | `IInteractable`, `LeverInteractable`, `PickupInteractable` | [interactables-items-conveyors.md](.claude/docs/interactables-items-conveyors.md) |
| Conveyors | `ConveyorPath`, `ConveyorNode`, `ConveyorObjectMover`, `ConveyorManager` | [interactables-items-conveyors.md](.claude/docs/interactables-items-conveyors.md) |
| Audio (FMOD) | `AudioManager`, `AudioAgent`, `FMODEvents` | [audio.md](.claude/docs/audio.md) |
| Architecture / how it all connects | — | [architecture.md](.claude/docs/architecture.md) |
| Layers, tags, scenes, prefabs, models, anims, editor tools, packages | — | [reference.md](.claude/docs/reference.md) |

---

## 4. Quick conventions / gotchas

- **Input:** gameplay uses the **legacy `Input` Manager** (WASD, mouse position/scroll, Space).
  The new **Input System** package *is installed* and `InputSystem_Actions.inputactions` exists,
  but gameplay does **not** use it yet. Don't assume the action asset is live.
- **AI does not use NavMesh.** The AI Navigation package is installed, but workers navigate a
  **custom `Waypoint` graph with BFS** (`Pathfinding.FindPath`), not Unity NavMesh.
- **Worker BT runs from scratch each frame**: `WorkerAIController.Update()` calls
  `_tree.Root.Execute(_blackboard)` every frame; state lives in a `Dictionary<string,object>`
  blackboard, not in node fields.
- **Layers are load-bearing.** Collision/impulse logic and detection masks key off specific
  layer indices (e.g. Worker=16, Ragdoll=17, Wall=15, Prop=7, Interactable=12, Player=3).
  See [reference.md](.claude/docs/reference.md#layers) before changing layer assignments.
- **`"Interactive"` tag** drives cursor hover + click-to-grab in `CursorController`; the
  `IInteractable` component on the hit object decides pickup vs. operate.
- Several scripts contain **`Debug.Log` spam and WIP/TODO regions** — that's expected on the
  demo branch; don't "clean them up" unprompted.

---

## 5. Tuning Hub registration rule

**Every new feature must register its main control variables in the Tuning Hub**
(`Tools ▸ Tuning Hub`).

When a new gameplay feature (or a meaningful new tunable on an existing system) is
implemented, add its primary tuning fields to
[Assets/Editor/TuningHub/TuningManifest.cs](Assets/Editor/TuningHub/TuningManifest.cs)
in the same change:

- Add `F(...)` entries in the matching category/group (or a new `G<T>(...)` group /
  `Cat(...)` category if the feature is a new system), with a friendly label, slider
  range, unit, and the correct apply-mode badge (`Live` / `Reapply` / `NextEvent` /
  `RestartPlay` / `EditorOnly` — check whether gameplay reads the field per-frame,
  caches it in Awake/Start, or reads it per-event).
- "Main control variables" = the knobs a designer would tweak (speeds, forces, timings,
  ranges, masses, toggles) — not object references, wiring, or runtime state flags
  (mark those `H(...)` if they're serialized).
- If a renamed/removed field breaks an existing entry, fix the manifest in the same
  change (the hub shows "field not found — update TuningManifest" rows and
  `Tools ▸ Tuning Hub ▸ Validate Manifest` lists all stale entries).
- Editing `TuningManifest.cs` is a method-body change → `echo reload > Library/hotReloadRemoteRequest.txt`.
- Never add an `.asmdef` to `Assets/Editor/TuningHub/` (breaks `typeof(...)` refs to
  gameplay classes — see the note at the top of TuningManifest.cs).
