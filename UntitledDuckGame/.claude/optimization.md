## Tier 0 — Real bugs, fix today (minutes each)

| # | Defect | Location | Impact |
|---|---|---|---|
| 1 | **Unguarded `using UnityEditor;` in two runtime scripts** | `WaypointTriggerVolume.cs:2`, `WaypointID.cs:2` | **Player builds fail with CS0246.** Verified: in WaypointID the `Handles.Label` call *is* inside `#if UNITY_EDITOR` but the `using` isn't; in WaypointTriggerVolume it's entirely unused. Delete one line, guard the other. |
| 2 | **Alarm levels L3–L7 are all zeros** | `AlarmLevelDataStandard.asset` | Above L2 every worker gets detection range 0 → **blind**, the inverse of intent. Also the array is sized 7 for an 8-value enum, so L7 clamps onto zeroed row 6, and `OuterCircleSpriteManager` divides by that 0 → NaN. |
| 3 | **ConveyorManager's 5 renderer refs are null in Demo** | `ConveyorManager.cs:24` | NRE in `Start` *and* every `Update` frame. Worse: it's **redundant** — `ConveyorSegmentVisual.cs` already replaced it per its own header comment, but the old component is still in the scene, so two systems fight over `_BaseMap_ST` and the old one leaks a material instance per renderer. Delete the component from Demo and the script. |
| 4 | **`RagdollActions` divides by zero with no guard** | `RagdollActions.cs:39` | `sum / ct` with `ct == 0` → NaN written to `transform.position`, which corrupts the transform irrecoverably. |
| 5 | **`AudioAgent.Play` → `AudioManager.instance.Get(key)`, no guards at any level** | `AudioAgent.cs:13`, `AudioManager.cs:25` | NRE in any scene without an AudioManager; `KeyNotFoundException` on any typo'd key. |
| 6 | **Unbox-null crashes** | `WaypointChecks.cs:11,22` | `(int)state.GetValueOrDefault("PathIndex")` throws NRE on a missing key. The sibling file uses the safe `is float s ? s : 0f` idiom for identical reads. |
| 7 | **`ctrl.PlayerTransform` (inspector field) ≠ `state["PlayerTransform"]` (detected player)** | `WorkerAIController.cs:32` vs `WaypointActions.cs:54` | Same name, different values. Both `LookAt` call sites dereference the unwired inspector field → NRE every frame a worker idles at a waypoint. Rename the field. |
| 8 | **`CubeSpawner` tints the prefab's shared material, discards the clone** | `CubeSpawner.cs:20-21` | Mutates the asset on disk. (Script is unused — deleting it also fixes it.) |

---

## Tier 1 — The one structural change that prevents recurrence

**Add assembly definitions and namespaces.** You currently have **zero asmdefs** and **zero namespaces** in first-party code — everything compiles into `Assembly-CSharp` in the global namespace.

This is the root enabler of most of what follows: nothing *can* enforce a module boundary, so coupling accumulated silently, and defect #1 above is invisible until you press Build. Four asmdefs is the whole job:

- `Duck.Core` — layers/tags/utils, no dependencies
- `Duck.Runtime` — `Scripts/**`, references Core + FMODUnity
- `Duck.Editor` — `Assets/Editor`, `includePlatforms: [Editor]`
- leave third-party alone (they already have their own)

Immediate payoffs: editor types in runtime code become a red squiggle instead of a build failure; script edits stop recompiling ConsolePro/LiteNetLib/TutorialInfo, so iteration gets faster; and once the boundary exists you can split Conveyors/Items/Audio/AI out one at a time and *watch* the coupling break.

---

## Tier 2 — Kill the silent-failure pattern

This is the single most damaging theme, and it cost you real debugging time twice today. **Missing configuration returns zeros instead of complaining.**

`GlobalAlarm.GetCurrentLevelData` returns `default` — a zero-filled struct — when the instance or config is null. Deep inside `DetectionChecks.cs:16` that becomes `OverlapSphere(pos, 0f, mask)`, and workers go blind with no log line. That's exactly why detection is dead in Demo.unity, and combined with Tier 0 #2 it's *also* why it would die above L2 even after you add the alarm object.

The fix is a policy, applied in three places: **fail loud at the boundary.**

1. `GetCurrentLevelData` — `Debug.LogError` once when instance/config is null, and return a sane fallback rather than zeros.
2. `AudioManager.Get` → `TryGet(key, out evt)`; `AudioAgent.Play` early-outs when `instance == null`.
3. `SetStateAtValue` — currently `if (_blackboard.ContainsKey(key))` silently drops writes to unseeded keys (`WorkerAIController.cs:256`). Log a warning on unknown keys.

On #3, one correction to flag since it's code I shipped today: I verified the `"CollisionStartTime"` re-stamp in `WorkerPickupInteractable.OnFlung` **does work** — `HandleCollision` adds the key by direct dictionary write and only `RecoverFromCollision` removes it, so the key is always present while a worker is ragdolled, which is the only state it can be held in. But it works by an invisible invariant, not by design. If recovery gating ever changes, it fails silently. Seeding `["CollisionStartTime"] = 0f` in `Awake` makes it robust.

Related: `GlobalAlarm`'s statics (`currentValue`, `lastIncreaseTime`, `Level`) are **never reset in Awake**, so with domain reload disabled the alarm level carries across play sessions. Same class of bug in `ItemEvents.cs` and `ConveyorNode.cs:6-8` — static events never unsubscribed, so subscribers from the previous session fire on destroyed objects. One `[RuntimeInitializeOnLoadMethod]` reset fixes all three.

---

## Tier 3 — Centralize the stringly-typed layer

Every identity in the project is expressed as a bare literal, in **five different idioms for layers alone**:

| Form | Example |
|---|---|
| raw index | `other.gameObject.layer == 3 // Player` — `ArmHitForwarder.cs:27` |
| string lookup per collision | `LayerMask.NameToLayer("Worker")` — `CubePropManager.cs:6` |
| raw bit literal as default | `heldItemLayer = 1 << 12` — `HeldItemController.cs:27` |
| hand-rolled mask test ×2 | `ObjectAbsorber.cs:59`, `WorkerAIController.cs:237` |
| 6 independent serialized `LayerMask` fields | WorkerAIController, PlayerDuckController, IsometricRaycaster ×3, DebugCanvasManager |

Same story for tags (`"Interactive"` in two files), animator params (`"isWalking"`, `"Activate"`, `"Deactivate"`, `"walk"`, `"idle"`), audio keys (`"ragdoll"`, `"playerHitWorker"`), shader properties (`"_BaseMap_ST"` re-hashed every frame in ConveyorManager, correctly cached in ConveyorSegmentVisual), and the 20-key BT blackboard.

**Fix:** a `Scripts/Core/` folder with `GameLayers`, `GameTags`, `ShaderIDs`, `WorkerAnimParams`, and `BB` (blackboard keys) — static classes resolving names once.

> ⚠️ **Landmine when you do this:** layer 12 is literally named `'Interactable '` **with a trailing space** in TagManager. `LayerMask.NameToLayer("Interactable")` returns **-1** and silently matches nothing. Fix the layer name first.

For the blackboard specifically, the minimal-churn version preserves your architecture completely — keep `Dictionary<string,object>`, add key consts plus two typed extension methods:

```csharp
public static T Get<T>(this Dictionary<string, object> s, string key, T fallback = default)
    => s.TryGetValue(key, out var v) && v is T t ? t : fallback;
```

That replaces all eleven `state.ContainsKey("X") && (bool)state["X"]` pairs, turns typos into compile errors, converts the unbox-null crashes into fallbacks, and changes **no node or builder signatures**. Worth it: the audit found `"PlayerSeenRange"` is **read but never written** (so `IsPlayerInRange` — the function your other session flagged — can only ever return false), and `"StartWaypoint"` is **written three times and read nowhere**.

---

## Tier 4 — Break the four coupling knots

**1. `IInteractable` names the concrete player arm in its own signature.** `IsometricRaycaster.cs:8-16` — the interface that exists to decouple interactables from the player takes `IsometricRaycaster arm`, and lives *inside that class's file*. So every interactable hard-depends on the player module and the file can never be renamed. Move it to `Interactables/IInteractable.cs` and narrow the parameter to an `IArmController` exposing only what interactables actually use.

**2. A world prop drives the player's CharacterController.** `LeverInteractable.cs:133` does `FindFirstObjectByType<PlayerDuckController>()` then calls `playerController.Move(...)` for 0.35s — while `PlayerDuckController.Update` may `Move` the same CC. Two owners of one controller, plus a hard single-player assumption. Give the player a `RequestMoveTo(pos, duration)` and take the reference from the `arm` parameter.

**3. UI owns interaction input.** `CursorController.cs:59` does the unmasked `Physics.Raycast`, the tag test, and calls `BeginHold`/`EndHold`. Delete the cursor UI and you delete the ability to pick anything up. Extract a `PlayerInteractor` on the player that raycasts (with a serialized mask and camera) and raises a hover event the cursor merely renders.

**4. Items ↔ conveyors ↔ player triangle.** `PickupInteractable.cs:42` holds a `ConveyorObjectMover` (so items can't compile without belts), while `ConveyorObjectMover` reads `PickupInteractable.pickupActive` twice per frame (so belts can't compile without the pickup module). Invert one arrow with a small `ICarryState` interface and the cycle breaks.

The good news: **you already have the right pattern in the project.** `ItemEvents.cs` is a static event bus where publishers don't know subscribers, and `ConveyorPath.All` is a self-registration registry populated in `OnEnable`. Routing worker hits and belt knock-offs through ItemEvents, and replacing the four `Find*ByType` calls with one generic `ComponentRegistry<T>`, would dissolve most of the above using patterns you wrote yourself.

---

## Tier 5 — De-duplication (mechanical, safe)

| Duplicated logic | Copies | Location |
|---|---|---|
| Reduced-mass impulse + worker-ragdoll block | **3** | `ArmHitForwarder.cs:36-64`, `HeldItemHitForwarder.cs:22-39`, `PlayerDuckController.cs:174-186` |
| Collider-bounds `Encapsulate` fold | **4** | ConveyorObjectMover ×2, HeldItemController:380, ObjectAbsorber:104 |
| Ragdoll/can-move guard clause | **4** | DetectionChecks:7, DetectionActions ×2, CollisionChecks/WaypointChecks |
| "Arrived at target" visual block | **2** | WaypointActions:49-54, WaypointChecks:29-34 |
| `ActionNode` / `CheckNode` | byte-identical | `BehaviourTreeNodes.cs:56-78` — make one derive from the other |
| `ShowDebugBox` overloads | 2× 60 lines | IsometricRaycaster:802, :832 — differ only by a rotation |
| `SampleByDistance` / `PositionAtDistance` | same walk loop | ConveyorPath:177, :222 |
| `WorkerPickupInteractable` copying its base | — | My code. Cleaner: add `protected virtual bool ReportsItemEvents => true;` to the base and call `base.OnHoldStart()` |

The guard-clause one is notable: it should be expressed **in the tree** as an `Inverter` prefix on the detection branch — that's what a behaviour tree is *for* — which deletes it from all four leaves.

---

## Tier 6 — God classes

`IsometricRaycaster.cs` is **869 lines doing six jobs**: mouse→yaw input, IK target composition, collision sweeps with binary search, a generic two-bone IK solver, the interaction state machine, and static debug-draw helpers. Two extractions are pure wins with zero behaviour risk, because both are stateless: move the IK solver into `static class TwoBoneIKSolver`, and the debug drawing into `static class DebugDraw` (that second one also removes `ArmHitForwarder`'s dependency on the raycaster, which exists *solely* to draw a gizmo).

Similar, smaller: `PlayerDuckController` mixes tutorial break-out state, locomotion, hand-rolled gravity, mouth-rig coroutines, and impulse response — extract `DuckMouthRig`. And `HeldItemController` animates the duck's bill and knows about conveyors; both belong behind events/interfaces.

---

## Tier 7 — Performance (all in per-frame paths)

| Cost | Location |
|---|---|
| `Physics.OverlapSphere` allocating a fresh array per worker per frame | `DetectionChecks.cs:19`, RagdollChecks.cs:19 → use `OverlapSphereNonAlloc` (the buffer pattern already exists in PlayerDuckController:61) |
| **9 linear segment walks per belt item per physics step** | ConveyorObjectMover:157,189,232 → precompute cumulative lengths + binary search |
| Full bounds fold every `FixedUpdate` per item | ConveyorObjectMover:214 |
| `FindClosestS` — thousands of segment iterations, looped over **every belt** | ConveyorPath:241, only survivable because throttled to 0.2s |
| A fresh ~40-node BT allocated **per worker** | WorkerAIController:117 — every node is stateless, so one `static` shared tree serves all workers |
| Boxing on every blackboard write | `LastDetectionTime` boxes a float *every frame* the player is visible |
| `FindObjectsByType<Waypoint>` full-scene scan on every repath | WorkerAIController:295 |
| `new List<>()` every frame, usually empty | DebugCanvasManager:62 |
| `GetComponent<Image>()` on every color set, every frame | CursorController:123-130 |
| `rend.material` (not `sharedMaterial`) → leaks an instance per access | ConveyorManager:42, WorkerVisualController:62 |
| `GlobalAlarm.Update` recomputes an unchanged enum every frame | GlobalAlarm:21 — delete it; `RequestIncrease` already calls it |
| LINQ + string interpolation in a release-build log | WaypointActions:18 |

Also free: **your physics layer collision matrix is entirely unused** — all `f`s, everything collides with everything. Turning off pairs that can never interact (Ragdoll↔Ragdoll, Interactable↔Arm, etc.) is free CPU *and* makes the layer scheme self-documenting instead of living only in code masks.

On logging: ~66 `Debug.Log` calls total. Per your CLAUDE.md I'm not suggesting a cleanup — but four are in genuinely hot paths and worth guarding: `WaypointChecks.cs:43` logs on *every evaluation* during recovery; `ItemEvents.logEvents` defaults to `true` and builds an interpolated string with a `LayerToName` call on every item impact; and both hit forwarders interpolate strings on every collision, unguarded.

---

## Tier 8 — Dead code (verified zero callers)

**Whole files:** `BehaviourTreeChecks.cs`, `RotationActions.cs`, `WaypointAgent.cs` (138 lines, self-labelled DEPRECATED), `CubeSpawner.cs`, `ForceReporter.cs` (its method is named `CollisionEnter`, not `OnCollisionEnter`, so it never fired), `TestTriggerVolume.cs`, `ObjectAbsorber.cs`.

**Members:** `WaypointBTBuilder.CreateTree`, `AnimationActions.GetWalking`, `WaypointChecks.HasMoreWaypointsCheck`, `DetectionActions.chaseReAcquireCooldown`, `WorkerAIController.ragdollRBEnabled`, `GlobalAlarm.decayRate` (so the alarm can only ratchet up, never decay), `AlarmConfig.LevelCount`, `FMODEvents.instance` (a second singleton nothing reads), `CursorController.cursorHit` + `outerMoveVelocity`, `IsometricRaycaster.uiCanvas`/`boundaryColor`/`fadeDistance`, `ArmHitForwarder.Mrb`, `HeldItemHitForwarder.Init`'s `itemCols` parameter, `BeginHold`/`EndHold`'s unused `PlayerDuckController` parameter, plus stray usings (`System.Security.Cryptography`, `System.Diagnostics`, `JetBrains.Annotations`, `System.Linq`).

Also: `BehaviourTree` is a no-op `MonoBehaviour` holding a public `Node` that `WorkerAIController` immediately overwrites and ticks itself — `[RequireComponent]` forces it onto every worker prefab for zero behaviour.

---

## Tier 9 — Fragility worth knowing about

- `originalFBX = transform.GetChild(0)` (positional child lookup — adding any child above the mesh silently retargets the ragdoll reset)
- the ragdoll rigidbody set is discovered by "any child RB", which the code's own comment admits breaks if a worker holds something
- the pelvis is "the one bone without a CharacterJoint" with no count check if a rig change creates two
- `BTInputManager`'s inspector-wired arrays can't see runtime-spawned workers and share one `lastIndex` across all of them
- waypoint **names are parsed as data** (`name[^1]`), so IDs ≥ 10 break debug output and any rename breaks it
- unreachable waypoints strand a worker permanently with no timeout or log
- `GlobalAlarm`'s duplicate handling destroys only the component, leaving statics shared

**Housekeeping:** three live scripts sit loose in `Assets/` root (`DebugCameraMover`, `DebugCanvasManager`, `OuterCircleSpriteManager`) and `BallSpinner.cs` is a generic prop filed under `Player Scripts/`. Move via Unity's Project window so GUIDs survive. Also, `.editorconfig` says **tabs** but every file is **4-space** — one of the two should change, since right now any tooling that honors the config will fight the codebase.
