# Reference — layers, tags, scenes, prefabs, models, animations, editor tools, packages

## Layers
From `ProjectSettings/TagManager.asset`. **Indices matter** — collision/impulse/detection code
keys off them directly (see `ArmHitForwarder`, `PlayerDuckController`, masks on workers).

| Index | Layer | Notable use |
| --- | --- | --- |
| 0 | Default | — |
| 1 | TransparentFX | — |
| 2 | Ignore Raycast | — |
| 3 | Player | QuackBot |
| 4 | Water | — |
| 5 | UI | — |
| 6 | Waypoint | Waypoint nodes |
| 7 | Prop | Pushable props (impulse targets) |
| 11 | Arm | Player arm colliders |
| 12 | `Interactable ` | Interactable objects *(note: name has a trailing space)* |
| 13 | Ground | Floor (cube-drop raycasts) |
| 14 | AICollision | AI collision helpers |
| 15 | Wall | Blocks arm rotation/IK sweeps |
| 16 | Worker | Worker robots (collision → ragdoll, arm-hit audio) |
| 17 | Ragdoll | Active ragdoll bodies |
| 31 | Debug | Temp debug spheres etc. |

> ⚠️ Layer index **12 is `"Interactable "` with a trailing space**. Use the exact string
> (or the index) — `LayerMask.NameToLayer("Interactable")` without the space returns -1.

## Tags
| Tag | Use |
| --- | --- |
| `PlayerIK` | (defined; IK-related tagging) |
| `Interactive` | Drives `CursorController` hover + `IsometricRaycaster` grab/operate routing |

## Scenes (`Assets/Scenes/`)
| Scene | Role |
| --- | --- |
| **`Demo.unity`** | **Main scene** — most complete; treat as the canonical game scene. |
| `WorkerAlarmDemo.unity` | Testbed for alarm + detection tuning. |
| `WorkerBTDemo.unity` | Testbed for behaviour-tree iteration. |

## Prefabs (`Assets/Prefabs/`)
- **Player:** `quackbot_dev.prefab`, `quackbot_three.prefab`
- **Workers:** `WorkerBase_PREFAB.prefab`, `WorkerHelmet_PREFAB.prefab`, `lowpoly_ragdoll.prefab`
- **Navigation:** `Waypoint.prefab`
- **Conveyor:** `ConveyorNode.prefab`, `1unit.prefab`
- **Props / world:** `drum.prefab`, `fan.prefab`, `floor.prefab`, `radio.prefab`, `radio_item.prefab`,
  `strut_base.prefab`, `wheely_cart.prefab`, `wheely_cart 1.prefab`, `wooden_pallet.prefab`,
  `Drop Cube.prefab`
- **UI:** `UI/Player UI Canvas.prefab`, `UI/UI Manager.prefab`, `UI/WorkerStatPanel.prefab`
- **VFX:** `VFX/MetalMetal Sparks Particles.prefab`

## Models (`Assets/Models/`, FBX)
`quackbot.fbx`, `cratebot.fbx`, `lowpoly_withrig.fbx` (worker, rigged), `helmet.fbx`,
`conveyor_belt.fbx`, `corner_belt.fbx`, `conveyor_item.fbx`, `drum.fbx`, `fan.fbx`, `bin1.fbx`,
`alarm_handle.fbx`, `item_lock.fbx`, `radio.fbx`, `strut_base.fbx`, `strut_main.fbx`,
`symbol_alarm.fbx`, `test_wall.fbx`, `wheely_cart.fbx`, `wooden_pallet.fbx`.

## Animations (`Assets/Animations/Worker/`)
- **Movement:** `lowpoly_withrig.controller`, `idle.anim`, `walk.anim`, `pray.anim`.
  BT checks/sets the `isWalking` bool and reads state names `"walk"` / `"idle"`.
- **Alert:** `workerAlert.controller`, `workerAlertActive.anim`, `workerAlertInactive.anim`,
  `workerAlertIdleOn/Off.anim`, `workerAlertWiggle.anim`. Driven by `Activate`/`Deactivate`
  triggers via `WorkerAIController.SetAlertAnimationActive`.

Player arm uses **Animation Rigging** (`TwoBoneIKConstraint`), not these clips.

## Editor tools (`Assets/Editor/`, under the **Tools** menu)
| Tool | Menu | Purpose |
| --- | --- | --- |
| `WaypointGraphGenerator.cs` | Tools ▸ Waypoint Graph Generator | Procedurally generate a waypoint graph (no dead ends) and assign to `BTInputManager`. |
| `ObjectValidator.cs` | Tools ▸ Object Validator | Report null serialized refs on a GameObject's components. |
| `FindObjectsOnLayer.cs` | Tools ▸ Find Objects On Layer | Select all scene objects on a chosen layer. |
| `FindMissingScripts.cs` | Tools ▸ Find Missing Scripts in Assets | Find prefabs with missing script components. |
| `HierarchyPrint.cs` | Tools ▸ Hierarchy Inspector | Dump a GameObject hierarchy + fields to `Assets/HierarchyDump.txt`. |
| `ChildAlphabeticalSorter.cs` | Tools ▸ Child Alphabetical Sorter | Sort a transform's children alphabetically (numeric-suffix aware). |

## Packages / tech (from `Packages/manifest.json`)
- **Unity 6000.0.71f1** (Unity 6).
- **URP** `17.0.4` (`render-pipelines.universal`) + **Amplify Shader Editor** (third-party, `Assets/AmplifyShaderEditor/`).
- **Animation Rigging** `1.4.1` (player arm IK).
- **Input System** `1.19.0` (installed; gameplay still uses legacy `Input`).
- **AI Navigation** `2.0.11` (installed; AI uses custom waypoints, **not** NavMesh).
- **Singularity Group Hot Reload** `1.13.21` (live C# reload — see CLAUDE.md rule 4).
- **TextMeshPro** (UGUI 2.0.0), **Vector Graphics**, **Timeline**, **Recorder**, **Visual Scripting**,
  **Test Framework**, **Multiplayer Center** (mostly default/unused for current gameplay).
- **FMOD for Unity** for audio (third-party plugin under `Assets/Plugins/`; banks in `StreamingAssets/`).
- **ConsolePro** (third-party enhanced console) under `Assets/ConsolePro/`.

## Loose root scripts (directly in `Assets/`, not under `Scripts/`)
- `DebugCameraMover.cs` — free iso camera pan (dev).
- `DebugCanvasManager.cs` — right-click selects workers (shows a `WorkerStatPanel` with `GetData()`),
  left-click drops a cube on the ground; can command selected workers via `BTInputManager`.
- `OuterCircleSpriteManager.cs` — worker detection ring sprite thickness (see [worker-ai.md](worker-ai.md)).

## Code style (`.editorconfig`)
Tabs (width 4), opening brace on **same line** (`csharp_new_line_before_open_brace = none`),
`using`s sorted system-first, UTF-8, LF, final newline, trim trailing whitespace.
