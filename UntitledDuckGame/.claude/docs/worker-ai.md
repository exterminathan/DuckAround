# Worker AI — behaviour tree, patrol, detection, ragdoll

Scripts: `Assets/Scripts/AI/`. Workers are the patrolling enemy robots.
Prefabs (in `Assets/Prefabs/AI/`): `WorkerBase_PREFAB.prefab`, `WorkerHelmet_PREFAB.prefab`,
`lowpoly_ragdoll.prefab` (models: `lowpoly_withrig.fbx`, `helmet.fbx`).
Animations: `Assets/Animations/Worker/`.

## Behaviour tree framework (`Behaviour Tree Scripts/`)

A small hand-rolled BT. **No external BT package.**

- `BehaviourTreeNodes.cs` — node types:
  - `Node` (abstract, `Execute(state) → bool`), `Composite` (holds children).
  - `Sequence` — runs children in order; returns false on first failure (AND).
  - `Selector` — returns true on first success (OR).
  - `Inverter` — negates a child.
  - `ActionNode` / `CheckNode` — wrap a `Func<Dictionary<string,object>, bool>`.
- `BehaviourTree.cs` — trivial MonoBehaviour holding `Node Root`.
- **State is a blackboard**: `Dictionary<string,object>` passed into every `Execute`. There is
  **no tick/running state** — the whole tree is re-evaluated every frame.

### Tree assembly (`*BTBuilder.cs`)
`PrimaryBTBuilder.CreateTree()` builds the root **Selector** (first succeeding branch wins):

```
WorkerBehaviour (Selector)
├─ CollisionHandler (Sequence)      ← CollisionBTBuilder.BuildCollisionSequence()
│    HasCollision? → NOT IsHeld? → SetIdle → HandleCollision → ResetPlayerUponRagdoll → EnterRagdoll
├─ RecoverHandler (Sequence)        ← CollisionBTBuilder.BuildRecoverySequence()
│    HasStopTimeElapsed? → IsRagdollActive? → NOT IsHeld? → NOT PlayerInSuppressRange?
│    → IsRagdollSettled? → ExitRagdoll → ResetWorkerPosition
│    → RecoverFromCollision → (if HasTargetWaypoint → SetWalking)
├─ Detection (Selector)             ← DetectionBTBuilder.BuildDetectionBranch()
│    ├─ ChaseBranch: IsChasing? → (LostPlayer? → EndChase) | (ChasePlayerTick → ensure Walking)
│    └─ AcquireBranch: DetectPlayer? → BeginChase
└─ WaypointTraversal (Sequence)     ← WaypointBTBuilder.BuildWaypointTraversal()
     IsAllowedToMove? → (IdleAtTarget | DoTravel[EnsurePath → SetWalking → PathTraversal])
```

The three carry guards *(2026-07-25)*: the collision branch never re-enters ragdoll while carried
(would un-kinematic the pelvis mid-carry), and recovery is blocked while carried, while the player
lingers within `RecoverySuppressRange`, or while the flung body is still moving faster than
`RecoverySettleSpeed` — so a worker never stands up in the bill, mid-air, or in the duck's face.

The order encodes priority: **ragdoll/recovery preempts everything**, then **chasing the
player**, then **patrol**.

### Actions & checks (static helper classes)
| File | Provides |
| --- | --- |
| `Animations/AnimationActions.cs` | `SetWalking`, `SetIdle`, `GetWalking` (toggle `isWalking` animator bool) |
| `Animations/AnimationChecks.cs` | `IsInWalkingState` / `IsInIdleState` (animator state name `"walk"`/`"idle"`) |
| `Collisions/CollisionChecks.cs` | `HasCollision`, `IsRagdollActive`, `HasStopTimeElapsed` (3s after collision) |
| `Collisions/CollisionActions.cs` | `HandleCollision` (freeze + flag ragdoll, stamp `CollisionStartTime`), `RecoverFromCollision` |
| `PlayerDetection/DetectionChecks.cs` | `DetectPlayer` (OverlapSphere + cone angle), `IsChasing`, `LostPlayer` (chase timer) |
| `PlayerDetection/DetectionActions.cs` | `BeginChase` (raises alarm, alert anim, chase visuals), `ChasePlayerTick`, `EndChase`, `ResetPlayerUponRagdoll` |
| `Ragdoll/RagdollActions.cs` | `EnterRagdoll`/`ExitRagdoll` (toggle `ApplyRagdoll`), `ResetWorkerPositionAfterRagdoll` (recenters root to ragdoll collider average) |
| `Ragdoll/RagdollChecks.cs` | carry guards *(2026-07-25)*: `IsHeldByPlayer` (blackboard `IsHeld`), `IsPlayerInSuppressRange` (OverlapSphere around the **pelvis** — the root transform stays at the knockout spot when the body is carried off), `IsRagdollSettled` (pelvis speed ≤ `RecoverySettleSpeed`) |
| `Transforms/RotationActions.cs` | `RotateToNextWaypointAction` (face next waypoint) |
| `Waypoints/WaypointChecks.cs` | `HasPathCheck`, `IsAllowedToMoveCheck`, `IsAtTargetWaypoint`, `HasTargetWaypoint` |
| `Waypoints/WaypointActions.cs` | `FindPathAction` (BFS), `PathTraversalAction` (move along path) |
| `BehaviourTreeChecks.cs` | generic helpers: `AlwaysTrue/False`, `IsPlayerInRange` |
| `BT Input Manager.cs` | `BTInputManager` — assigns random new destinations to workers (debug/commands) |

## WorkerAIController.cs
The per-worker MonoBehaviour (requires `BehaviourTree`). On `Awake` it:
- Caches child **rigidbodies** and **colliders** (by type) for the ragdoll.
- Builds the tree (`PrimaryBTBuilder.CreateTree()`) and seeds the **blackboard**.
- Calls `ApplyRagdoll(false)` to start in animated (non-ragdoll) mode.

`Update()` ticks `_tree.Root.Execute(_blackboard)` every frame.

**`ApplyRagdoll(bool on)`** — the ragdoll switch: disables the `Animator` when on, toggles each
child rigidbody `isKinematic`/`useGravity`, enables/disables ragdoll colliders, toggles the
primary collider, plays `"ragdoll"` audio, toggles the pickup adapter's `PickupAllowed` (grabbable
**only while down** — guards the click-on-recovery-frame race), and on **off** resets the FBX
local position and sets the patrol visual color.

**Carry / pickup** *(2026-07-25 — a downed worker can be carried and flung)* — on `Awake` the
controller finds the **pelvis** (the one bone rigidbody with no `CharacterJoint` — the ragdoll
root) and adds a `WorkerPickupInteractable` to it at runtime (see
[interactables-items-conveyors.md](interactables-items-conveyors.md)); the bone colliders are
already tagged `Interactive` in the prefab and only exist while ragdolled. The carry rides
`HeldItemController`'s **dangling** path (pelvis hangs off a joint, limbs keep flailing — see
[player.md](player.md)). Tuning lives on the controller's *Carry / Pickup* inspector header:
`RecoverySuppressRange`, `RecoverySettleSpeed` (the BT recovery guards), `CarryGripSize` /
`CarryGripOffset` / `CarryGripRotation` (default Z=90 — the worker rides sideways in the bill),
and `CarryRotationSpring` / `CarryRotationDamper` (bite stiffness: 0 = rigid, ~200 floppy →
~2000 firm; re-read on every grab, so play-mode tweaks apply to the next pickup).

**Collisions** — `OnCollisionEnter` logs contact info, plays `"ragdoll"`, and if the other layer
is in `workerCollisionLayerMask` sets `"IsCollided"=true` (→ ragdoll branch). External code
(player body, arm) also calls `SetStateAtValue("IsCollided", true)`.

Helpers: `SetStateAtValue(key, val)`, `SetAlertAnimationActive(bool)` (alert animator triggers),
`SetNewDestination(Waypoint)` (used by triggers/`BTInputManager`), `FindClosestWaypoint(pos)`,
`GetData()` (HUD string: target waypoint + alarm level for the debug panel).

### Blackboard keys
`SelfTransform`, `WorkerAIController`, `WorkerAnimator`, `AlertAnimator`, `StartWaypoint`,
`TargetWaypoint`, `FullPath`, `PathIndex`, `Speed`, `ArriveThreshold`, `IsAllowedToMove`,
`IsCollided`, `IsRagdollActive`, `CollisionStartTime`, `IsChasing`, `LastDetectionTime`,
`PlayerTransform`, `IsHeld` (written by `WorkerPickupInteractable` via `SetStateAtValue`),
`PelvisRigidbody`. **Keys are string literals — typos fail silently.**

## Waypoint navigation (`Waypoint Scripts/`)
- `Waypoint.cs` — node with a `List<Waypoint> neighbors`; draws gizmo lines (blue = bidirectional,
  red = one-way). `[ExecuteAlways]`.
- `WaypointID.cs` — labels/names a waypoint (`Waypoint - {id}`) and draws its id in the scene.
- `Pathfinding.cs` — static **BFS** `FindPath(start, goal)` over the neighbor graph (unweighted).
- `WaypointAgent.cs` — **DEPRECATED** (header says so). A standalone rigidbody mover along a
  destination list; superseded by `WorkerAIController` + the BT. Kept for non-BT objects.

Workers do **not** use Unity NavMesh; navigation is purely this waypoint graph.

## Detection visuals
- `WorkerVisualController.cs` — drives the floating inner/outer detection circles. Defines
  `enum StateName { IDLE, PATROL, ALERT, CHASING, RAGDOLL }` and a `ColorState[]` mapping.
  `SetVisualColor(state, opacityOff?)` lerps the circle colors; `SetVisualParameters(angle, distance)`
  lerps the cone arc (`_Arc1/_Arc2` shader params) and scales the inner circle (`ScaleFromUnits`).
  `SetVisualParametersToDefault()` reads `GlobalAlarm.GetDefaultLevelData()`.
- `OuterCircleSpriteManager.cs` (loose root script) — swaps the outer ring **sprite** by thickness,
  indexing `circleSprites[]` based on distance / `GlobalAlarm.GetMaxAlarmLevelData()`.

## Editor support
`Assets/Editor/WaypointGraphGenerator.cs` (menu **Tools ▸ Waypoint Graph Generator**) procedurally
spawns a waypoint graph (grid + Hamiltonian cycle for no dead ends + extra two-way/one-way edges)
and assigns a subset to the scene's `BTInputManager`. See [reference.md](reference.md#editor-tools).

## Gotchas
- BT branches mutate shared blackboard flags; re-ordering builder nodes changes priority/behaviour.
- `DetectPlayer` only checks **`hits[0]`** from the `OverlapSphere` (first overlapping collider on
  the `PlayerDetectionLayerMask`), and does a cone-angle test — not a line-of-sight raycast
  (no occlusion check by walls).
- Ragdoll relies on the child collider/rigidbody hierarchy from the rigged FBX; changing the rig
  affects `ApplyRagdoll` and `ResetWorkerPositionAfterRagdoll`.
