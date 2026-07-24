# Player — QuackBot, the arm/IK, and interaction

Scripts: `Assets/Scripts/Player Scripts/` (+ `Assets/Scripts/UI/CursorController.cs`).
Prefabs: `quackbot_dev.prefab`, `quackbot_three.prefab` (model: `Models/quackbot.fbx`,
`Models/cratebot.fbx`).

> **Current tuning values** (from the Demo-scene player prefab) are captured in
> [player-inspector-values.md](player-inspector-values.md).

## Control scheme (PC, mouse + keyboard)

| Input | Effect | Implemented in |
| --- | --- | --- |
| **WASD** | Move the bot around the map (isometric translation). | `PlayerDuckController.Update` |
| **Mouse X** (left/right) | Rotate the bot left↔right, **clamped between two angle ranges** (`min/maxPivotAngle`). Applied to `rotate_pivot` (its yaw / Y euler in code). | `IsometricRaycaster.HandleRotation` |
| **Mouse Y** (up/down) | Move the **IK target up and down** → the arm reaches higher/lower (`min/maxIKY`). | `IsometricRaycaster.HandleVerticalIK` |
| **Scroll wheel** | Move the **IK target toward the body / outward** along local X (`min/maxIKX`). | `IsometricRaycaster.HandleHorizontalIK` |
| **Left mouse (hold)** | Grab a `Pickup` (hold to carry; **releasing mid-flick flings it**) / operate an `Operate` interactable under the cursor. | `CursorController` → `IsometricRaycaster.BeginHold/EndHold` → `HeldItemController` |
| **Space** | Quack (opens/closes the mouth). | `PlayerDuckController.Quack` |

The mouse **Y** and the **scroll wheel** together fully determine the IK target position: mouse Y
sets its height, scroll sets its in/out reach. Mouse **X** rotates the whole bot/arm-pivot side to
side (it does **not** move the IK target).

### Arm-collision guards (sweeps)
To stop the arm meshes clipping through static geometry, the aiming inputs are guarded by *sweep*
checks that limit movement for the frame when a collision would occur:
- **Vertical (mouse Y):** `HandleVerticalIKSweep` — before applying the new IK-target height, it
  predicts the arm-bone positions (via `TwoBoneIKPreCalc`) and limits the target to the furthest
  height along the path that keeps the arm meshes clear of `verticalIKBlockingLayerMask`.
- **Horizontal / rotation (mouse X):** `HandleHorizontalIKSweep` — simulates the arm meshes at the
  rotated bot orientation and limits the rotation to the furthest angle that keeps them clear of
  blocking geometry. (Rotation-based; does **not** use `TwoBoneIKPreCalc`.)

See [status-roadmap.md](status-roadmap.md) for the current implementation state of these two sweeps.

## PlayerDuckController.cs
The core player MonoBehaviour. Requires a `CharacterController`.

**Break-free tutorial gate** — while `!isBrokenFree`, pressing W/A/S/D increments `keysPressed`;
after **>5** presses it sets `isBrokenFree = true`, hides `meshBase` (the restraint mesh),
enables `canTraverse`, and nudges the controller down by `rig_drop_distance` (−0.14). *(Intended
to be tutorial-only; see TODO in code.)*

**Movement** — isometric WASD. On `Start`, derives `isoForward`/`isoRight` from the iso camera's
forward (flattened to XZ). Each frame builds `dir = isoForward*input.z + isoRight*(input.x*horizontalSpeedFactor)`,
scales by `moveSpeed`, applies a `diagonalFactor` multiplier on diagonals, and moves the
`CharacterController`. Applies simple gravity (`-9.81`) via `vertVelocity` when not grounded.
Movement is suppressed while `isometricRaycaster.isInteracting` (i.e. operating a lever).
While carrying, `CarrySpeedMultiplier` (runtime property, set from held-item mass by
`HeldItemController`) scales the move speed — see carry encumbrance below.

**Shoving physics** — `OnControllerColliderHit` handles collisions with the **bot's base**:
- If the other object has a `WorkerAIController`, sets its `"IsCollided"` blackboard flag (→ ragdoll).
- If the other object has a non-kinematic `Rigidbody`, applies an impulse using reduced mass
  `μ = robotMass*m2/(robotMass+m2)` along the contact normal, scaled by `bodyImpulseDampFactor`,
  gated by `pushThreshold`. Arm collisions are handled separately by `ArmHitForwarder`.

**Mouth / quack** — `Quack()` (Space) and `ToggleMouth(open, duration)` coroutines rotate the
`mouth` transform by `quackRotation`; quacking while holding an item also releases it.
`OpenMouthTo(angle, duration)` eases to an explicit gape — used by `HeldItemController` for a
size-adaptive mouth opening while carrying.

Notable serialized refs: `rigTarget`, `root`, `mouth`, `meshBase`, `isometricRaycaster`,
`isoCamera`, `armColliders[]`, `audioAgent`, and physics tuning (`robotMass`,
`bodyImpulseDampFactor`, `armImpulseDampFactor`, `pushThreshold`).

`HandleMovementSweep()` exists but is currently a no-op stub.

## IsometricRaycaster.cs
The **arm controller + interaction broker** — the most complex player script. Defines the
interaction contracts:

```csharp
public enum InteractionType { None, Pickup, Operate }
public enum HoldMode { None, Pickup, Interact }
public interface IInteractable {
    InteractionType Type { get; }
    void OnHoldStart(RaycastHit hit, Transform rigTarget);
    void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta);
    void OnHoldEnd();
}
```

**Arm aiming (when not operating something):**
- `HandleRotation()` — maps mouse **X** within `±innerZoneRangeX` of screen center to a target
  Y angle (`min/maxPivotAngle`), smoothed by `rotationSmoothSpeed`, applied to `rotate_pivot`.
  Before applying, **sweeps each arm `BoxCollider`** with `Physics.OverlapBox` against
  `rotationBlockingLayerMask`; if any overlap, rotation is blocked that frame.
- `HandleVerticalIK()` — maps mouse **Y** within `±innerZoneRangeY` to a target height
  (`min/maxIKY`), recomposes the full body-local target (scroll reach + height + fixed z) and
  eases it in (`ikVerticalSmoothSpeed`). Guarded by `HandleVerticalIKSweep()` (implemented — see
  [status-roadmap.md](status-roadmap.md)).
- `HandleHorizontalIK()` — mouse **scroll** moves `ik_target.localPosition.x` by `scrollIncrement`,
  clamped to `min/maxIKX`.
- `LateUpdate()` computes per-arm velocity for each `ArmHitForwarder` (`velocity = Δpos/Δt`),
  used for shove-impulse math.
- **Carry encumbrance hook:** `ArmSpeedMultiplier` (runtime property, set from held-item mass by
  `HeldItemController`; 1 = unencumbered) scales both the rotation and vertical ease rates.

**Two-bone IK math:** `TwoBoneIKPreCalc(...)` re-implements Unity's `TwoBoneIKConstraint` solve
(law of cosines + hint bend plane) to *predict* bone positions for sweep checks; it also draws
debug lines/spheres. The live IK is still applied by Unity's `TwoBoneIKConstraint` (`tbikc`).

**Interaction lifecycle:**
- `BeginHold(hit, player)` — looks up `IInteractable` on the hit collider. `Operate` → `HoldMode.Interact`
  (sets `isInteracting`, rotates arm toward target, snaps IK to hit point, calls `OnHoldStart`).
  `Pickup` → `HoldMode.Pickup` (calls `OnHoldStart`). Fallback by `Interactive` tag if no component.
- `HandleHoldInteraction()` (while `Interact`) — re-raycasts the held collider and forwards
  `OnHoldDrag(hit, mouseDelta)`.
- `EndHold(player)` — calls `OnHoldEnd`, restores pre-hold rotation/IK for `Interact`, clears state.

Key refs: `mainCamera`, `playerDuckController`, `ik_target`, `bone_point`, `rotate_pivot`,
`tbikc` (TwoBoneIKConstraint), `armObjects[]`, `playerHoldSlot` (the bill-tip anchor a held item
follows — items are **not** reparented; see `HeldItemController` below), and three blocking
`LayerMask`s.

## HeldItemController.cs (+ HeldItemHitForwarder.cs)
Owns the **held-item lifecycle** (2026-07 overhaul). Lives on the same `Rig_target` GameObject as
`IsometricRaycaster`; `PickupInteractable` reaches it via `HeldItemController.GetOrAdd(arm)` — no
prefab wiring. States: `None → Transit → Held`.

**Carry** — `Grab(item)`: detaches from any belt (`DetachForPickup`), ignores collisions vs the
player's arm/body only (item colliders stay **live** — a carried item is a weapon), swaps all child
layers to `heldItemLayer` (a `LayerMask` — tick exactly one; default **Interactable**, must stay
out of the arm sweep masks), makes the rigidbody kinematic (gravity + interpolation off), tweens it
into the bill over `transitDuration` (0.15 s), then **hard-follows `playerHoldSlot` every
`LateUpdate`, post-IK**. The item is never reparented. Mouth gape scales with item size via
`OpenMouthTo`. `HeldItemHitForwarder` (added at grab, destroyed at release) mirrors
`ArmHitForwarder` for the carried item: ragdolls workers, shoves props with the item's tracked
velocity (same reduced-mass μ).

**Fling on release** — while `Held`, the item's world position is ring-buffered per frame (it rides
the bill, so the buffer naturally combines arm height + body yaw + WASD motion). `Release()` (the
single exit for mouse-up, sticky-click and quack) averages the hand velocity over
`flingSampleWindow` (0.12 s):
- below `minFlingSpeed` (1.5 u/s) → **gentle place** (zero velocity, like a plain drop — keeps
  careful conveyor placement intact);
- otherwise launch = flick × `flingPowerScale` (1.2) × a momentum falloff anchored to the
  **Mass Scale**: an item at `maxCarryMass` (250) keeps `flingKeepAtMaxMass` (0.15) of the flick,
  lighter items curve toward 1 (cube 1 ≈ 0.98, drum 20 ≈ 0.69, shelf 75 ≈ 0.37);
- clamped to `maxFlingSpeed` (15), plus end-over-end tumble (`flingTumbleFactor`, rad/s per m/s).
A 0.3 s `armIgnoreSeconds` grace stops the arm punching the item on the way out.

**Encumbrance** — at grab, item mass maps to t ∈ [`encumberLightMass` (1) → `maxCarryMass`]; two
**runtime** (non-serialized) multipliers ease toward per-channel floors:
`PlayerDuckController.CarrySpeedMultiplier` (WASD; floor `minCarryMoveMobility` 0.8) and
`IsometricRaycaster.ArmSpeedMultiplier` (yaw + vertical ease; floor `minCarryArmMobility` 0.8).
Master toggle `enableEncumbrance`; both reset to 1 on every release/cleanup path.

## ArmHitForwarder.cs
On each arm collider. `OnCollisionEnter` logs the hit by **layer** (Default/Player/Prop/
Interactable/Wall/Worker/Ragdoll) and, for non-kinematic rigidbodies, applies a shove impulse
using the arm's tracked `velocity` and reduced mass (mirrors the body formula but uses
`armImpulseDampFactor`). On hitting a Worker it plays `"playerHitWorker"` and sets the worker's
`"IsCollided"` flag. `velocity`/`lastPos` are written each frame by `IsometricRaycaster.LateUpdate`.

## CursorController.cs (UI)
Custom software cursor (hides the OS cursor). Smooth-damps `innerCursor` to the mouse; raycasts
each frame, and when it hits an `Interactive`-tagged collider **within `hoverEngageDistance`** of
the player (measured flat/XZ), it grows + recolors the `outerCursor`. On mouse-down while hovering
in range → `isometricRaycaster.BeginHold(hit, player)`; on mouse-up while holding →
`EndHold(player)`. `SetCursorColor(color, flag)` (0=both, 1=inner, 2=outer). *(Note: it
`using Mono.Cecil.Cil;` — an unused/stray import.)*

## BallSpinner.cs
Trivial cosmetic: spins the transform around a random axis at `spinSpeed` deg/s (e.g. for a
ball/wheel prop).

## DebugCameraMover.cs (loose root script, `Assets/DebugCameraMover.cs`)
Free isometric camera pan with WASD (inverted), independent of the player — a dev convenience.

## Gotchas
- Carry/fling/encumbrance tuning lives on `HeldItemController` (Mass Scale / Fling / Encumbrance
  inspector headers). Values are read **at grab time** — mid-carry tweaks apply on the next pickup.
  The two mobility multipliers are runtime properties (never serialized), so they can't stick
  after release.
- `heldItemLayer` (LayerMask — tick exactly one) must stay OUT of the arm sweep masks, or the
  sweeps clamp against the carried item and freeze the arm.
- Movement & arm both read **legacy `Input`**; the arm "dead zone" and reach feel are tuned via
  the many serialized ranges — change those in the inspector, not hardcoded, when iterating.
