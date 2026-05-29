# Status & Roadmap — what works vs. what's planned

Snapshot of mechanic status on the **demo branch** (as of 2026-05). Confirmed with the developer.
Check here before building on a mechanic — several described features are **not wired up yet**.

## ✅ Working / functional
- **Break-free intro** — WASD-mash to release QuackBot (`PlayerDuckController.isBrokenFree`).
- **Isometric movement** — WASD via `CharacterController`.
- **Arm aiming / IK** — mouse-driven pivot rotation + vertical reach + scroll extension, with
  arm-rotation collision sweeps (`IsometricRaycaster`).
- **Shove physics** — body & arm impulses on rigidbodies/workers (reduced-mass formula).
- **Worker AI** — patrol (BFS waypoints), vision-cone detection, chase, alert animation, visuals.
- **Ragdoll-on-hit** — shoving/hitting a worker ragdolls it; auto-recovers after ~3s. *(Intended to stay.)*
- **Global alarm escalation** — rises on detection (cooldown-gated); scales detection range/angle,
  chase timer, chase speed; HUD readout + detection-circle visuals.
- **Conveyors** — path building (straight + corner), item movement, end-of-belt fling, belt UV scroll.

## 🐞 Buggy / needs work
- **Object pickup & drop** (`PickupInteractable` + `ConveyorObjectMover` hand-off). The grab,
  reparent-to-mouth, drop, and conveyor re-snap interactions are unreliable. **Known priority fix.**

## 🚧 Planned / not yet wired
- **Throwing objects with force.** Items can be picked up and dropped, but there is no throw
  impulse on release yet.
- **Lever → alarm reset.** `LeverInteractable` rotates but connects to nothing. It should lower/
  reset `GlobalAlarm` (which also has **no auto-decay** yet) and route through the general
  `IInteractable` system the developer is building out.
- **Object-based worker destruction.** Real "throw a prop to destroy/knock out a worker" is planned.
  The current `CubePropManager` (cube self-destructs when it touches a worker) is a **placeholder**.
- **General interactable system** consolidation — levers and future operables should all go through
  `IInteractable` cleanly.
- **Tutorial-gating** — the WASD break-free spam is meant to be tutorial-only (see TODO in
  `PlayerDuckController`).

## Arm collision sweeps — implemented (2026-05)
- `IsometricRaycaster.HandleVerticalIKSweep(Vector3)` — guards the **mouse-Y IK height**. Predicts
  the arm pose via `TwoBoneIKPreCalc` and box-checks both segments (root→mid, mid→tip) against
  `verticalIKBlockingLayerMask`; binary-searches the furthest clear height and clamps to it.
  Boxes are oriented to each bone (+Z along the segment) with tunable width/height and a
  `armSweepBoxRotationOffset`; rendered via the rotated `ShowDebugBox` overload (`drawSweepDebug`).
- `IsometricRaycaster.HandleHorizontalIKSweep(float)` — guards the **mouse-X bot rotation**. Rotates
  the arm `BoxCollider`s about `rotate_pivot` (world yaw) and binary-searches the furthest clear
  angle; **replaced** the old all-or-nothing inline block in `HandleRotation`. Uses
  `rotationBlockingLayerMask`. (Does not use `TwoBoneIKPreCalc` — it's whole-bot rotation.)
- Tuning fields live on `IsometricRaycaster`: `armSweepBoxWidth`, `armSweepBoxHeight`,
  `armSweepBoxRotationOffset`, `sweepIterations`, `drawSweepDebug`.

## Known stubs / scaffolding in code (intentional, don't "fix" unprompted)
- `IsometricRaycaster.HandleRotationSweep` / `PlayerDuckController.HandleMovementSweep` — empty
  helper stubs (the rotation guard now lives in `HandleHorizontalIKSweep`).
- `GlobalAlarm.decayRate` — serialized but unused (no decay applied).
- `AudioAgent.PlayEvent` — no dynamic emitter fallback (TODO comment).
- Verbose `Debug.Log` instrumentation throughout AI/physics — expected on the demo branch.
- `WaypointAgent.cs` — **deprecated**; use `WorkerAIController` + BT instead.

## Working notes
- Active branch is a **demo branch**; recent commits are demo tooling/checkpoints.
- When picking up a mechanic from "Planned", confirm scope with the developer first (see CLAUDE.md
  rules 1–2: don't assume, don't edit unauthorized files).
