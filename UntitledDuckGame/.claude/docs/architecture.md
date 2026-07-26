# Architecture — how the systems connect

High-level map of the runtime systems and the data/control flow between them. For per-script
detail, see the system docs ([player](player.md), [worker-ai](worker-ai.md), [alarm](alarm.md),
[interactables-items-conveyors](interactables-items-conveyors.md), [audio](audio.md)).

## Big picture

```
                         ┌─────────────────────────────────────────────┐
                         │                Demo.unity scene              │
                         └─────────────────────────────────────────────┘
        PLAYER SIDE                                   ENEMY / WORLD SIDE
 ┌───────────────────────┐                    ┌──────────────────────────────┐
 │ CursorController (UI)  │                    │ WorkerAIController (per worker)│
 │  raycast mouse →       │                    │  builds BT (PrimaryBTBuilder)  │
 │  hover/click Interactive│                   │  ticks Root.Execute(blackboard)│
 └─────────┬──────────────┘                    └───────┬───────────────┬───────┘
           │ BeginHold/EndHold                         │ reads          │ writes visuals
           ▼                                            ▼                ▼
 ┌───────────────────────┐   sweeps/IK     ┌────────────────────┐  ┌───────────────────────┐
 │ IsometricRaycaster     │◄──────────────►│ Pathfinding (BFS on │  │ WorkerVisualController │
 │  arm rotation + 2-bone  │                │ Waypoint graph)     │  │  + OuterCircleSprite   │
 │  IK + interaction broker│                └────────────────────┘  └───────────────────────┘
 └─────────┬──────────────┘                         ▲
           │ owns / references                       │ detection range/angle, chase speed
           ▼                                          │
 ┌───────────────────────┐                    ┌──────┴───────────┐     ┌───────────────────┐
 │ PlayerDuckController    │  shoves rb /     │   GlobalAlarm     │◄────│ AlarmConfig (SO)   │
 │  CharacterController     │  ragdolls worker │  static L0–L7     │     │ per-level data      │
 │  + ArmHitForwarder[]     │─────────────────►│  RequestIncrease()│     └───────────────────┘
 └─────────┬──────────────┘                    └──────┬───────────┘
           │ Play("...")                               │ Level → string
           ▼                                            ▼
 ┌───────────────────────┐   key lookup     ┌────────────────────┐
 │ AudioAgent (per actor) │─────────────────►│ AudioManager        │ → FMOD StudioEventEmitter
 └───────────────────────┘                   │  (singleton, FMODEvents)
                                              └────────────────────┘

 WORLD OBJECTS: ConveyorPath/Node/ObjectMover carry items; Items (Cube/Fan/etc.) are props.
```

## Control flow per frame

- **Player:** `CursorController.Update` raycasts the mouse; on hover over a collider tagged
  `Interactive` (and within `hoverEngageDistance` of the player) it enlarges the cursor and, on
  mouse-down, calls `IsometricRaycaster.BeginHold(hit, player)`. `IsometricRaycaster.Update`
  either drives the **arm** (rotation + vertical/horizontal IK, with collision sweeps) **or**, while
  holding an `Operate` interactable, forwards drag to the interactable. `PlayerDuckController.Update`
  reads WASD and moves the `CharacterController` in isometric space. `HeldItemController.LateUpdate`
  (same GameObject as the raycaster) poses any carried item at the bill slot **post-IK** and samples
  its world velocity — the source of the fling applied on release.
- **Worker:** `WorkerAIController.Update` calls `_tree.Root.Execute(_blackboard)`. The tree is a
  `Selector` of: **collision/ragdoll → recovery → detection/chase → waypoint patrol** (first
  succeeding branch wins). All mutable state (waypoints, flags, timers, animator refs) lives in the
  blackboard `Dictionary<string,object>`.

## Cross-system contracts (the important glue)

| From → To | Mechanism | Notes |
| --- | --- | --- |
| Player body/arm/held item → Worker | `OnControllerColliderHit` / `ArmHitForwarder` / `HeldItemHitForwarder` call `worker.SetStateAtValue("IsCollided", true)` | Triggers the worker's collision→ragdoll BT branch. |
| Worker detection → Alarm | `DetectionActions.BeginChase` calls `GlobalAlarm.RequestIncrease(1f)` | Alarm rises on each (cooldown-gated) new acquisition. |
| Alarm → Worker behaviour & visuals | `GlobalAlarm.GetCurrentLevelData()` read in `DetectionChecks`/`DetectionActions` and `WorkerVisualController` | Detection angle/distance, chase timer, chase speed all scale with level. |
| Player/Worker → Audio | `AudioAgent.Play("ragdoll" / "playerHitWorker")` → `AudioManager.Get(key)` → matching `StudioEventEmitter` | Keys are mapped in `AudioManager.Awake`. |
| Interactable ↔ Player | `IInteractable` (`Pickup` / `Operate`) brokered by `IsometricRaycaster` hold state | `CursorController` initiates; raycaster routes drag/hold lifecycle. |
| Conveyor ↔ Pickup | `PickupInteractable.pickupActive` gates the mover (belt fully yields while held); `DetachForPickup()` on grab; free items re-snap to any belt after settling | Carry is owned by `HeldItemController` (post-IK follow at `playerHoldSlot`, **no reparenting**); release flings with the sampled arm velocity. |
| Player carry → Worker | `WorkerPickupInteractable` (runtime-added to the pelvis) writes `"IsHeld"` / `"CollisionStartTime"` via `SetStateAtValue`; `RagdollChecks` gate the BT recovery branch | A downed worker rides `HeldItemController`'s **dangling** joint-carry path (`DanglingCarry` on the `PickupInteractable` base); `OnFlung` launches the other bones and restarts the recovery timer. |

## Key design choices to respect

- **Hand-rolled behaviour tree**, not a package. Nodes (`Sequence`, `Selector`, `Inverter`,
  `ActionNode`, `CheckNode`) are in `BehaviourTreeNodes.cs`; trees are assembled by static
  `*BTBuilder` classes; logic lives in static `*Actions` / `*Checks` classes that read/write the
  blackboard. To change worker behaviour, edit the builders and the action/check methods.
- **Blackboard keys are strings** (e.g. `"WorkerAIController"`, `"FullPath"`, `"IsChasing"`,
  `"PlayerTransform"`). Typos fail silently. Keep keys consistent with the table in
  [worker-ai.md](worker-ai.md#blackboard-keys).
- **Custom waypoint navigation** (BFS over `Waypoint.neighbors`), **not** NavMesh.
- **Legacy `Input`** for gameplay (not the Input System asset).
- **Physics impulses use a reduced-mass formula** `μ = m1*m2/(m1+m2)` in both
  `PlayerDuckController.OnControllerColliderHit` and `ArmHitForwarder` — keep them consistent if
  you tune one.
