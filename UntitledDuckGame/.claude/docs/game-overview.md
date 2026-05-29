# Game Overview — DuckAround

> Tentative public title **DuckAround**; internal/repo name **UntitledDuckGame**.
> Unity 6 (6000.0.71f1), URP, PC desktop (mouse + keyboard), FMOD audio.

## Concept

A 3D **isometric sabotage** game set in a factory. You are **QuackBot**, a duck-shaped
factory robot. The fantasy: a captive worker-bot that breaks loose and gleefully
**disrupts production** — knocking out the patrolling Worker robots, throwing objects to
destroy them, and messing up the assembly line — while a facility-wide **alarm/alert level**
ramps up against you.

## Core loop (intended)

1. **Break free.** QuackBot starts restrained. Mashing **WASD** (>5 key presses) snaps it
   loose (`isBrokenFree`), drops it onto the floor, and enables movement. *(This break-out
   spam is intended to be tutorial-only.)*
2. **Move + manipulate.** Walk isometrically (WASD). Aim and articulate the **arm** with the
   mouse (rotate / reach / extend) and **click-drag** to grab pickups or operate machinery.
   **Space** = quack (flavor / opens mouth).
3. **Sabotage.** Shove/hit Workers to **ragdoll** them; (planned) **throw** props to destroy
   Workers and disrupt the conveyor production line.
4. **Manage the alarm.** Each time a Worker spots you, the **global alarm** rises. Higher
   alarm = Workers see **farther and wider** and **chase faster**. (Planned) reset/lower the
   alarm by operating **levers**.

## The two main actors

### QuackBot (player)
- Duck-shaped robot with an **articulated arm** driven by **Two-Bone IK** (Animation Rigging).
- Mouse controls the arm: **X position → pivot rotation**, **Y position → vertical reach (IK)**,
  **scroll → horizontal extension**. Click-drag on a tagged `Interactive` object grabs/operates.
- Has a **mouth** that opens to "hold" picked-up items and animates when quacking.
- Moves via a `CharacterController`; shoving rigidbodies/Workers applies physics impulses.
- See [player.md](player.md).

### Workers (enemies/NPCs)
- Helmeted patrol robots driven by a hand-rolled **behaviour tree** over a shared blackboard.
- **Patrol** a custom **waypoint graph** (BFS pathfinding), **detect** the player via an
  `OverlapSphere` + vision-cone angle check, **chase**, and **ragdoll** when collided/hit.
- A floating **vision indicator** (inner/outer circle) recolors and resizes by state
  (idle / patrol / alert / chasing) and by alarm-driven range/angle.
- See [worker-ai.md](worker-ai.md).

## Supporting systems

- **Global Alarm (L0–L7)** — singleton alert meter; an `AlarmConfig` ScriptableObject maps each
  level to detection angle/distance, chase timer, and chase speed. See [alarm.md](alarm.md).
- **Conveyors** — data-driven belt paths that carry item objects and fling them off the end.
  See [interactables-items-conveyors.md](interactables-items-conveyors.md).
- **Interactables** — `IInteractable` interface with `Pickup` and `Operate` (lever) implementations,
  brokered by the player's `IsometricRaycaster`. See [interactables-items-conveyors.md](interactables-items-conveyors.md).
- **Audio** — thin FMOD wrappers (`AudioManager` → `AudioAgent` → `StudioEventEmitter`).
  See [audio.md](audio.md).

## Status snapshot

This is an **active demo-branch prototype**. The aim (sabotage) is decided, but several
described mechanics are **not implemented yet** and some are **buggy**. Before building on a
mechanic, check [status-roadmap.md](status-roadmap.md). Highlights:

- ✅ Working: break-free, iso movement, arm IK aiming, worker patrol/detect/chase, ragdoll-on-hit,
  alarm escalation + visuals, conveyors.
- 🐞 Buggy / needs work: **object pickup & drop** (`PickupInteractable` / `ConveyorObjectMover`
  hand-off).
- 🚧 Planned / not wired: **throwing** objects with force, **lever → alarm reset** (levers
  currently just rotate and connect to nothing; should route through the general
  `IInteractable` system being set up), object-based **worker destruction** (current
  `CubePropManager` self-destruct on worker hit is a placeholder).
