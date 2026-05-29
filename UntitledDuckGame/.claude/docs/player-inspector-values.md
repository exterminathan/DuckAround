# Player inspector values (Demo scene)

Current serialized values for the player scripts, captured **2026-05-28**.

**Source of truth:** In `Demo.unity` the player is an instance of **`Assets/Prefabs/quackbot_three.prefab`**
(prefab guid `461fa2cfff867d54d99d77cab50afda3`, PrefabInstance fileID `1513513973`). The scene has
**no field overrides** on the player scripts, so the prefab values below are exactly what runs in Demo.
If you change these in the prefab, every scene using it updates; to change just Demo, override on the
scene instance.

> ⚠️ Keep this file in sync by hand — it's a snapshot, not generated. Re-capture after retuning.

## PlayerDuckController (`&318518283601257330`)
| Field | Value | Notes |
| --- | --- | --- |
| `quackRotation` | 30 | mouth open angle (deg) |
| `quackDuration` | 0.1 | quack anim time (s) |
| `isBrokenFree` | false (0) | starts restrained |
| `playerBlockingLayerMask` | bits `32769` → **Default(0) + Wall(15)** | |
| `moveSpeed` | 10 | |
| `diagonalFactor` | 1 | (range 0.5–2; 1 = no diagonal boost) |
| `verticalMinClamp` / `verticalMaxClamp` | 0 / 3.3 | |
| `horizontalMinClamp` / `horizontalMaxClamp` | -3 / -1 | |
| `robotMass` | 100 | used in shove impulse μ |
| `bodyImpulseDampFactor` | 0.05 | body-collision shove scale |
| `armImpulseDampFactor` | 0.2 | arm-hit shove scale |
| `horizontalSpeedFactor` | 0.5 | iso horizontal compensation |
| `armColliders` | 2 entries | the two arm box colliders |
| Object refs | `rigTarget`, `root`, `mouth`, `meshBase`, `isometricRaycaster`, `isoCamera`, `audioAgent` all assigned | |
| `pushThreshold` | *(not serialized)* | it's a runtime `{ get; set; }` property — set in code, not the inspector |

**CharacterController** (same GameObject): height `1.93`, radius `0.39`, skinWidth `0.039`,
center `(0, 1.1, 0)`, slopeLimit `45`, stepOffset `0`, minMoveDistance `0.001`. *(Note: `Start()`
also forces `skinWidth = 0.039` in code.)*

## IsometricRaycaster (`&752357334695128801`)
| Field | Value | Notes |
| --- | --- | --- |
| `innerZoneRangeX` | 225 | mouse-X dead-zone half-width (px) for rotation |
| `minPivotAngle` / `maxPivotAngle` | -90 / 90 | rotation clamp |
| `rotationSmoothSpeed` | 1.5 | |
| `minIKX` / `maxIKX` | -2.65 / -1.4 | scroll in/out reach (local X) |
| `scrollIncrement` | 0.5 | scroll → IK X step |
| `innerZoneRangeY` | 200 | mouse-Y dead-zone half-height (px) for vertical IK |
| `minIKY` / `maxIKY` | 0.05 / 2.562 | IK target height range |
| `ikVerticalSmoothSpeed` | 5 | |
| `boundaryColor` | (0.633, 1, 0.458, 1) | |
| `fadeDistance` | 50 | |
| `rotationBlockingLayerMask` | bits `32768` → **Wall(15)** | mouse-X rotation sweep |
| `horizontalIKBlockingLayerMask` | bits `32896` → **Prop(7) + Wall(15)** | (scroll IK; not currently swept) |
| `verticalIKBlockingLayerMask` | bits `32896` → **Prop(7) + Wall(15)** | mouse-Y vertical sweep |
| `armObjects` | 2 entries | upper arm + forearm roots (source of `armColliders`) |
| Object refs | `mainCamera`, `playerDuckController`, `ik_target`, `bone_point`, `rotate_pivot`, `playerHoldSlot` assigned; `uiCanvas` = none (found at runtime) | |
| `tbikc` (TwoBoneIKConstraint) | *(not in this block)* | assigned elsewhere / found via rig |

**New arm-sweep fields** were just added to the script and are **not yet serialized in the prefab** —
they show editor defaults until you tune and save them. Re-capture this table once set. Inspector
groups: **Upper Arm Box Sweep**, **Bottom Arm Box Sweep**, **Arm Box Sweep - Shared**.

| Field (per arm: `upperArmBox*` / `foreArmBox*`) | Default | Meaning |
| --- | --- | --- |
| `*Width` / `*Height` | 0.1 / 0.1 | box cross-section (local X / Y) |
| `*RotationOffset` | (0,0,0) | rotation to align box with the mesh (box +Z runs along the bone) |
| `*CenterOffset` | (0,0,0) | shifts the box off the bone midpoint, **in the box's local axes** (X=width dir, Y=height dir, Z=along bone) |
| `sweepIterations` (shared) | 8 | binary-search steps when a sweep is blocked (precision ≈ move / 2^iterations) |
| `drawSweepDebug` (shared) | true | draw the predicted sweep boxes (green=clear, red=blocked) |

*(A stale `showDev: 0` value is still serialized in the prefab from a removed field — harmless; will
clear on next prefab re-save.)*

## ArmHitForwarder (×2 — one per arm collider object)
| Field | Value | Notes |
| --- | --- | --- |
| `player` | assigned (→ PlayerDuckController) | |
| `lastPos` / `velocity` | (0,0,0) | runtime-only; written each frame by `IsometricRaycaster.LateUpdate` |

No tunable parameters.

## BallSpinner (`&2684157906524464634`)
| Field | Value |
| --- | --- |
| `spinSpeed` | 720 (deg/s) |

## Not present on the Demo-scene player
- **CursorController** — lives on a UI object/prefab (`Prefabs/UI/Player UI Canvas.prefab` /
  `UI Manager.prefab`), not on the quackbot prefab. Its values aren't captured here; ask if you want
  them pulled from the UI prefab.

## Layer-mask decode reference
`m_Bits` is a bitfield of layer indices: `2^15 = 32768` (Wall), `2^7 = 128` (Prop), `2^0 = 1` (Default).
So `32896 = 32768 + 128` = Prop + Wall; `32769 = 32768 + 1` = Default + Wall. See
[reference.md](reference.md#layers) for the full layer list.
