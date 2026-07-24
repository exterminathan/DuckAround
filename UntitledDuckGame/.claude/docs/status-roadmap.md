# Status & Roadmap — what works vs. what's planned

Snapshot of mechanic status on the **demo branch** (as of 2026-07-24). Confirmed with the developer.
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
- **Conveyors** — path building (straight + corner), **physical belt riding** (on-belt items are
  dynamic bodies driven by a FixedUpdate velocity servo), ride-pose capture, any-belt re-snap
  (runtime registry, no pre-wiring), belt-owned settings (`speed`/`loop`/`exitForce`/`beltWidth`/
  `maxItemMass`), end-of-belt fling, belt UV scroll. *(Reworked 2026-07.)*
- **Held-item carry** — `HeldItemController`: grab → transit tween → post-IK hard-follow at the
  bill slot (**no reparenting**); carried items keep live colliders (they shove props / ragdoll
  workers via `HeldItemHitForwarder`); clean belt hand-off both ways. *(Overhauled 2026-07-22/23.)*
- **Fling / throw on release** — flick the arm and let go: windowed hand-velocity launch with a
  mass falloff anchored to `maxCarryMass`, gentle-place dead zone, speed cap, tumble. *(2026-07-24;
  see [player.md](player.md).)*
- **Carry encumbrance** — heavier held items scale down duck move speed and arm ease rates
  (per-channel floors, master `enableEncumbrance` toggle). *(2026-07-24.)*

## 🐞 Buggy / needs work
- **Item grab loc/rot not fully working** — a grabbed item's position/orientation in the bill isn't
  always right (per-item `gripOffset` / hold-slot rotation needs work; items ride exactly at
  `holdSlot` pose + offset today).
- **Demo.unity scene drums** — the two belt drums ("drum (5)", "drum (14)") are hand-built from the
  raw FBX rather than `drum.prefab` (dead concave MeshCollider, stray tiny BoxCollider, wrong
  layer/mass); being repaired in-editor. `drum.prefab` itself is correct.
- **Feel-tuning pass pending** — the fling + encumbrance knobs on `HeldItemController` (Mass Scale /
  Fling / Encumbrance inspector headers) and the belt snap tolerances are fresh defaults, not
  final tuning.

## 🚧 Planned / not yet wired
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
