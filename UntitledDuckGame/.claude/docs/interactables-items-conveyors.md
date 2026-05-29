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

### PickupInteractable.cs (`Type = Pickup`) — 🐞 buggy, needs work
On `OnHoldStart`: opens the mouth (`ToggleMouth`), zeroes velocity, makes the rigidbody kinematic,
**reparents the object to `isometricRaycaster.playerHoldSlot`** at local origin, and releases it
from a conveyor if it was on one. On `OnHoldEnd`: closes mouth, re-enables gravity/physics,
unparents. `Update` watches for the held object dropping below `y < 0.875` to re-activate a
conveyor mover (`ReactivateFromWorldDrop`). **This pickup/drop + conveyor hand-off is the known
buggy area** — treat it as in-progress.

### LeverInteractable.cs (`Type = Operate`) — 🚧 rotates only, not wired
`OnHoldStart` records the pivot rotation; `OnHoldDrag` accumulates mouse-Y into an angle
clamped between `min/maxAngle` and applies it about `localAxis`; `OnHoldEnd` springs back to 0.
`GetNormalizedValue()` returns a 0–1 value. **Currently the lever's value connects to nothing** —
it should eventually drive things like resetting `GlobalAlarm`, via the general interactable
system. Model: `Models/alarm_handle.fbx`.

## Items (`Assets/Scripts/Items/`)
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
  in-editor; draws path/corner gizmos.
- **`ConveyorObjectMover.cs`** — put on an item. Advances a distance `s` along the path at `speed`,
  sampling position + tangent (with `maxTurnRateDegPerSec` smoothing) each frame. `loop` repeats;
  non-loop **releases the item with force** (`ReleaseFromConveyorWithForce`, applies `exitForce`
  along the tangent and disables itself). Integrates with pickup: when an item is picked up it
  detaches; on drop near the belt (`snapDistance`) it re-snaps via `ReactivateFromWorldDrop`,
  otherwise stays a free rigidbody. Toggles between an interaction `BoxCollider` (on belt) and a
  `MeshCollider` (free). Item model: `conveyor_item.fbx`; belts: `conveyor_belt.fbx`, `corner_belt.fbx`.
- **`ConveyorManager.cs`** — scrolls the belt **material UV offset** (`_BaseMap_ST`) on the belt /
  corner `MeshRenderer`s to fake motion; separate tiling/offset for straight vs. corner belts.

### Spawners / triggers for items
- `Debug/UnitSpawner.cs` — press **Tab** to spawn a random `units[]` prefab and assign it the
  conveyor `cPath`.
- `Debug/ObjectAbsorber.cs` — a bin/collector: parents colliding objects (after a delay, with an
  emission fade), and unparents them when they leave its bounds or it tilts past 135°. Model: `bin1.fbx`.

## Gotchas
- Pickup ↔ conveyor hand-off is **buggy** (see roadmap). The re-snap logic depends on `snapDistance`,
  rigidbody kinematic toggling, and collider swapping being in sync.
- Corner generation in `ConveyorPath` assumes roughly equal leg lengths (`isProper` check) — odd
  node placement produces straight fallbacks.
- Conveyor belt **visual** scroll (`ConveyorManager`) and item **movement** (`ConveyorObjectMover`)
  are independent — keep their directions/speeds consistent when tuning.
