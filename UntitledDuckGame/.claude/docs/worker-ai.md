# Worker AI — behaviour tree, patrol, detection, ragdoll

Scripts: `Assets/Scripts/AI/`. Workers are the patrolling enemy robots.
Prefabs: `WorkerBase_PREFAB.prefab`, `WorkerHelmet_PREFAB.prefab`, `lowpoly_ragdoll.prefab`
(models: `lowpoly_withrig.fbx`, `helmet.fbx`). Animations: `Assets/Animations/Worker/`.

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
│    HasCollision? → SetIdle → HandleCollision → ResetPlayerUponRagdoll → EnterRagdoll
├─ RecoverHandler (Sequence)        ← CollisionBTBuilder.BuildRecoverySequence()
│    HasStopTimeElapsed? → IsRagdollActive? → ExitRagdoll → ResetWorkerPosition
│    → RecoverFromCollision → (if HasTargetWaypoint → SetWalking)
├─ Detection (Selector)             ← DetectionBTBuilder.BuildDetectionBranch()
│    ├─ ChaseBranch: IsChasing? → (LostPlayer? → EndChase) | (ChasePlayerTick → ensure Walking)
│    └─ AcquireBranch: DetectPlayer? → BeginChase
└─ WaypointTraversal (Sequence)     ← WaypointBTBuilder.BuildWaypointTraversal()
     IsAllowedToMove? → (IdleAtTarget | DoTravel[EnsurePath → SetWalking → PathTraversal])
```

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
primary collider, plays `"ragdoll"` audio, and on **off** resets the FBX local position and sets
the patrol visual color.

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
`PlayerTransform`. **Keys are string literals — typos fail silently.**

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
