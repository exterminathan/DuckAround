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
bounds) and `gripOffset` (carry offset in bill space). `pickupActive` is the "currently held" bridge
flag the conveyor mover checks to fully yield control during a carry. `Body` exposes the item's
`Rigidbody` — its `mass` is the input for fling falloff and encumbrance.

### LeverInteractable.cs (`Type = Operate`) — 🚧 rotates only, not wired
`OnHoldStart` records the pivot rotation; `OnHoldDrag` accumulates mouse-Y into an angle
clamped between `min/maxAngle` and applies it about `localAxis`; `OnHoldEnd` springs back to 0.
`GetNormalizedValue()` returns a 0–1 value. **Currently the lever's value connects to nothing** —
it should eventually drive things like resetting `GlobalAlarm`, via the general interactable
system. Model: `Models/alarm_handle.fbx`.

## Items (`Assets/Scripts/Items/`)
- `ItemEvents.cs` — static event hub for item lifecycle: `PickedUp`, `Dropped`, `ItemImpact`,
  `BeltCaptured`, `BeltLeft(item, path, reason)` with `BeltLeaveReason { PickedUp, KnockedOff, Flung }`.
- `CubeSpawner.cs` — debug: press **P** to instantiate `instance` at the spawner with a random color.
- `CubePropManager.cs` — on a prop cube; **destroys itself** when it collides with a `Worker`-layer
  object. *(Placeholder for "object destroys/knocks out worker"; real sabotage destruction is planned.)*
- `FanRotator.cs` — spins the transform about local X at `fanSpeed` (cosmetic fan). Model: `fan.fbx`.

(Other props: `drum`, `radio`, `radio_item`, `wooden_pallet`, `wheely_cart`, `strut_base` — prefabs
in `Assets/Prefabs/`, models in `Assets/Models/`.)

## Conveyors (`Assets/Scripts/Conveyors/`)
A data-driven belt system that carries item objects along a path and flings them off the end.

- **`ConveyorNode.cs`** `[ExecuteAlways]` — a single control point. Exposes `Position`, `Forward`,
  a `turnDirection` (0 = straight, ±1 = corner), and a `DebugOn` flag. Fires static events
  (`OnAnyNodeMoved`, `OnDebugActivated/Deactivated`) so paths rebuild live in-editor. Draws gizmos.
  Prefab: `ConveyorNode.prefab`.
- **`ConveyorPath.cs`** — builds an ordered list of `ISegment`s from its `nodes`. Straight runs
  become `StraightSegment`s; nodes flagged with a `turnDirection` (and equal leg lengths) generate
  an **arc** approximated by `cornerSubdiv` straight segments (polar math around a computed center).
  Provides samplers: `SampleByDistance(s)`, `SampleByDistanceSmoothed(s, halfWindow)` (tangent via
  finite difference), `PositionAtDistance(s)`, and `TotalLength`. Rebuilds on node move/validate
  in-editor; draws path/corner gizmos. Also owns the **Belt Settings** (`speed`, `loop`,
  `exitForce`, `beltWidth` — wide belts allow capture/riding anywhere across the width — and
  `maxItemMass`, 0 = unlimited) plus a static runtime registry `ConveyorPath.All` (populated in
  `OnEnable`, which also rebuilds at runtime so standalone builds work).
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
- **`ConveyorManager.cs`** — scrolls the belt **material UV offset** (`_BaseMap_ST`) on the belt /
  corner `MeshRenderer`s to fake motion; separate tiling/offset for straight vs. corner belts.

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
- Conveyor belt **visual** scroll (`ConveyorManager`) and item **movement** (`ConveyorObjectMover`)
  are independent — keep their directions/speeds consistent when tuning.
