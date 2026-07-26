# Interactables, Items & Conveyors

Scripts: `Assets/Scripts/Interactables/`, `Assets/Scripts/Items/`, `Assets/Scripts/Conveyors/`.

## Interactables (`IInteractable` system)

The interaction contract lives in `IsometricRaycaster.cs` (see [player.md](player.md)):

```csharp
enum InteractionType { None, Pickup, Operate }
interface IInteractable {
    InteractionType Type { get; }
    void OnHoldStart(RaycastHit hit, Transform rigTarget);
    void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta);
    void OnHoldEnd();
}
```

Flow: `CursorController` raycasts the mouse → if it hits an `Interactive`-tagged collider within
range, mouse-down calls `IsometricRaycaster.BeginHold`, which finds the `IInteractable` and routes
the hold based on `Type` (`Operate` → arm locks to it and forwards drag; `Pickup` → grab).

### PickupInteractable.cs (`Type = Pickup`)
A **thin adapter** — the actual carry/fling lives in `HeldItemController` (see
[player.md](player.md), 2026-07 overhaul). `OnHoldStart`: `HeldItemController.GetOrAdd(arm).Grab(this)`
+ fires `ItemEvents.PickedUp`. `OnHoldEnd`: `holder.Release()` (which **flings** the item if the arm
was moving) + `ItemEvents.Dropped`. Per-item tuning: `gripSize` (mouth gape; 0 = auto from collider
bounds), `gripOffset` (carry offset in bill space) and `gripRotation` (extra euler on top of the
hold-slot rotation while carried). `pickupActive` is the "currently held" bridge flag the conveyor
mover checks to fully yield control during a carry. `Body` exposes the item's `Rigidbody`.

**Virtual hooks for special carriers** *(2026-07-25 — added for the worker carry)*:
- `CarryMass` — effective mass for encumbrance + fling falloff (default `Body.mass`; multi-body
  carriers sum their parts).
- `DanglingCarry` — `true` = carried as a dangling multi-body hung from a kinematic anchor via a
  joint instead of the kinematic transform-lock (see `HeldItemController` in [player.md](player.md)).
- `DanglingRotationSpring` / `DanglingRotationDamper` — bite stiffness for a dangling carry:
  `<= 0` = rigid grip (FixedJoint); `> 0` = spring-driven rotation (ConfigurableJoint slerp drive),
  so the body sags/swings in the mouth.
- `OnFlung(flingVelocity)` — invoked by `HeldItemController` on **every** release, right after the
  fling velocity is applied to `Body` — multi-body carriers launch their remaining rigidbodies here.
- `OnHoldStart`/`OnHoldEnd`/`OnCollisionEnter`/`Awake` are virtual; `holder` is `protected`.

### WorkerPickupInteractable.cs (`Type = Pickup`) — carry a ragdolled worker *(2026-07-25)*
Subclass of `PickupInteractable`, **added at runtime** to the worker's pelvis bone by
`WorkerAIController` (never placed in prefabs). Rides the dangling-carry path (`DanglingCarry =
true`) so the jointed limbs keep flailing while the pelvis sits in the bill. `CarryMass` sums all
bone rigidbodies; bite-stiffness knobs proxy to the controller's `CarryRotationSpring/Damper`;
grip tuning is re-read from the controller on every grab. `PickupAllowed` (toggled by
`ApplyRagdoll`) gates grabs to downed workers only. Talks to the worker BT via
`SetStateAtValue("IsHeld", …)` and restamps `"CollisionStartTime"` in `OnFlung` so the recovery
timer restarts from the release. No `ItemEvents` in either direction (a worker is not a
score/conveyor item, and ragdoll thuds would spam impacts). See [worker-ai.md](worker-ai.md).

### LeverInteractable.cs (`Type = Operate`) — full grab/drag feel, value still not wired
Reworked 2026-07-25 (and still being tuned — expect uncommitted iteration on this file). Flow on
grab: capture the grabbed point in pivot-local space → compute an **arm-aligned stand spot** in
the lever's swing plane (perpendicular to `localAxis`, side chosen by where the duck already is,
flipped if the arm pivot can't reach — `CanArmAim`) → **roll** the duck there via the
CharacterController (`rollSpeed`, capped `maxRollDuration`; skipped inside `snapTolerance`) →
`Engage()`: arm aims at the lever (`SetInteractArmAim` — the **body/camera never turn**), hand
eases onto the grab point over `handReachTime` and rides it un-parented via `SetArmTargetWorld`.
Dragging is **anchor-relative**: cursor travel from the grab-time position maps into the remaining
stroke (`travelScreenFraction` of screen height per full stroke, `invertDrag`), and the lever
follows **rate-limited** — `maxLeverSpeed × tightness × tightnessCurve(progress)` — so it feels
heavy instead of snapping. Release springs back to the **authored rest pose** (captured once in
`Awake`; `returnToRestOnRelease`, `returnSpeed`). `GetNormalizedValue()` returns 0–1.
**The value still connects to nothing** — the planned hookup is resetting `GlobalAlarm`.
Model: `Models/alarm_handle.fbx`.

## Items (`Assets/Scripts/Items/`)
- `ItemEvents.cs` — static event hub for item lifecycle: `PickedUp`, `Dropped`, `ItemImpact`,
  `BeltCaptured`, `BeltLeft(item, path, reason)` with `BeltLeaveReason { PickedUp, KnockedOff, Flung }`.
- `CubeSpawner.cs` — debug: press **P** to instantiate `instance` at the spawner with a random color.
- `CubePropManager.cs` — on a prop cube; **destroys itself** when it collides with a `Worker`-layer
  object. *(Placeholder for "object destroys/knocks out worker"; real sabotage destruction is planned.)*
- `FanRotator.cs` — spins the transform about local X at `fanSpeed` (cosmetic fan). Model: `fan.fbx`.

(Other props: `drum`, `radio`, `radio_item`, `wooden_pallet`, `wheely_cart_2_shelf`,
`wheely_cart_3_shelf`, `strut_base` — prefabs in `Assets/Prefabs/Props/`, models in
`Assets/Models/`. Prefabs were re-organized into subfolders 2026-07-25 — see
[reference.md](reference.md#prefabs).)

## Conveyors (`Assets/Scripts/Conveyors/`)
A data-driven belt system that carries item objects along a path and flings them off the end.

- **`ConveyorNode.cs`** `[ExecuteAlways]` — a single control point. Exposes `Position`, `Forward`,
  a `turnDirection` (0 = straight, ±1 = corner), and a `DebugOn` flag. Fires static events
  (`OnAnyNodeMoved`, `OnDebugActivated/Deactivated`) so paths rebuild live in-editor. Draws gizmos.
  Prefab: `Map Features/ConveyorNode.prefab`.
- **`ConveyorPath.cs`** — builds an ordered list of `ISegment`s from its `nodes`. Straight runs
  become `StraightSegment`s; nodes flagged with a `turnDirection` (and equal leg lengths) generate
  an **arc** approximated by `cornerSubdiv` straight segments (polar math around a computed center).
  Provides samplers: `SampleByDistance(s)`, `SampleByDistanceSmoothed(s, halfWindow)` (tangent via
  finite difference), `PositionAtDistance(s)`, `FindClosestS(worldPos)` (closest track distance —
  coarse scan + refine; used by mover capture and segment visuals), and `TotalLength`. Rebuilds on
  node move/validate in-editor; draws path/corner gizmos. Also owns the **Belt Settings** (`speed`,
  `loop`, `exitForce`, `beltWidth` — wide belts allow capture/riding anywhere across the width —
  and `maxItemMass`, 0 = unlimited) plus a static runtime registry `ConveyorPath.All` (populated in
  `OnEnable`, which also rebuilds at runtime so standalone builds work).
  - **Auto-assemble** *(2026-07-25)*: with `autoCollectNodes` on, the node list is rebuilt from
    `ConveyorNode`s found in **children, hierarchy (depth-first) order = travel order** — drop the
    segment prefabs (`conveyor_straight` / `conveyor_corner` / `conveyor_end`, in
    `Assets/Prefabs/Map Features/`; straights carry 1 node, corners 2) under the path object in
    travel order and it assembles itself. `loop` re-appends the first node as the closure. The
    hand-wired `nodes` list is ignored/overwritten while on. Newly dropped child nodes rebuild the
    path immediately (`IsChildOf` check in the node-moved handler).
  - **Reverse Path Direction** *(editor-only button — `Assets/Editor/ConveyorPathEditor.cs`)*:
    reverses travel direction in place. Flips child order (auto-collect) or the hand-wired list,
    rotates every node 180° about Y (no flipped prefab variants exist), and migrates each corner's
    `turnDirection` to its new entry node with the sign mirrored. Undo-aware,
    multi-object-editable. Code lives in `ConveyorPath.ReversePathDirection()` inside
    `#if UNITY_EDITOR`.
- **`ConveyorObjectMover.cs`** — put on an item. Explicit `OnBelt`/`Free` states. On-belt items
  ride as **dynamic bodies** (gravity off), driven in `FixedUpdate` by a soft velocity servo
  (belt-speed feedforward + proportional pull toward a lead point on the path); track position is a
  **monotone progress cursor** advanced by measured motion along the tangent (no closest-point
  re-sync — that's ambiguous at straight/corner transitions). Rotation + lateral/height offset are
  captured as a **ride pose** on every capture, so items ride however they were placed. Shoved past
  `snapDistance` (+0.25) off the lane → released as `KnockedOff`; non-loop path ends **fling**
  (`ReleaseFromConveyorWithForce`, belt `exitForce` along the tangent). `Free` items re-snap to
  **any** belt (`ConveyorPath.All` — no pre-wiring) after a 0.5 s cooldown, **sustained** near-rest
  (0.25 s, linear + angular), within `snapDistance` of a lane and `snapHeightTolerance` vertically;
  belts refuse items above their `maxItemMass`. `DetachForPickup()` hands off to the held-item
  system without touching the rigidbody; `KnockOff(impulse, point)` is the directed shove release.
  Per-item fields are tolerances only — `speed`/`loop`/`exitForce` live on the **path**.
  Item model: `conveyor_item.fbx`; belts: `conveyor_belt.fbx`, `corner_belt.fbx`.
- **`ConveyorSegmentVisual.cs`** *(2026-07-25 — replaces the old global `ConveyorManager`)* —
  per-segment belt visual, lives on each segment prefab. Scrolls the segment's `_BaseMap` V offset
  at the parent path's `Speed` (`uvPerMeter` calibrates texture pace to riding items). Tiling +
  base offset are read from the material asset at startup; `.material` instancing keeps the shared
  `.mat` untouched. Scroll **sign is auto-derived** from the path tangent at the segment
  (`localTravelAxis` dotted against it; manual `Forward`/`Reverse` override for ambiguous
  placements — near-perpendicular tangents warn and fall back). No parent `ConveyorPath` = static
  decor: it disables itself and never scrolls. Runtime-only (no edit-mode preview).
- **`ConveyorManager.cs`** — **superseded** by `ConveyorSegmentVisual`; the old global UV scroller
  is no longer referenced by anything. Don't build on it.

### Spawners / triggers for items
- `Debug/UnitSpawner.cs` — press **Tab** to spawn a random `units[]` prefab (spawned with the
  prefab's own rotation; no path wiring needed — any belt captures items placed on it).
- `Debug/ObjectAbsorber.cs` — a bin/collector: parents colliding objects (after a delay, with an
  emission fade), and unparents them when they leave its bounds or it tilts past 135°. Skips
  currently-held items (`pickupActive` guard). Model: `bin1.fbx`.

## Gotchas
- Belt re-snap needs **sustained** near-rest (0.25 s) + a 0.5 s cooldown + lane/height proximity —
  a flung or shoved item never recaptures mid-flight; a gentle release (slow hand → zero-velocity
  place) settles and captures. While an item is held (`pickupActive`), the mover fully yields —
  never drive the rigidbody from belt code during a carry.
- Corner generation in `ConveyorPath` assumes roughly equal leg lengths (`isProper` check) — odd
  node placement produces straight fallbacks.
- Conveyor belt **visual** scroll (`ConveyorSegmentVisual`) and item **movement**
  (`ConveyorObjectMover`) are independent — the visual reads the path's `Speed` and auto-signs
  from the tangent, but `uvPerMeter` is a per-prefab calibration; re-check it if belt textures or
  tiling change.
- `ReversePathDirection` on a multi-node segment prefab instance can't sibling-reorder nodes
  **inside** the segment (prefab restriction) — it swaps their transform/turn data instead. If a
  reversed corner looks wrong, check the two nodes inside the corner segment first.
