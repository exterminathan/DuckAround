# DuckAround — documentation index

Detailed docs for the **DuckAround** (internal: *UntitledDuckGame*) Unity project.
Start with the root [CLAUDE.md](../../CLAUDE.md) for working rules and the repo map.

## Read in this order
1. **[game-overview.md](game-overview.md)** — what the game is, the core loop, the two actors.
2. **[architecture.md](architecture.md)** — how all the systems connect (diagrams + contracts).
3. System deep-dives:
   - **[player.md](player.md)** — QuackBot controller, the IK arm, interaction broker, cursor.
     (Tuning snapshot: **[player-inspector-values.md](player-inspector-values.md)**.)
   - **[worker-ai.md](worker-ai.md)** — behaviour tree, waypoints/BFS, detection, chase, ragdoll.
   - **[alarm.md](alarm.md)** — global alert level L0–L7 and its config.
   - **[interactables-items-conveyors.md](interactables-items-conveyors.md)** — `IInteractable`, pickups, levers, items, conveyors.
   - **[audio.md](audio.md)** — FMOD wrappers.
4. **[reference.md](reference.md)** — layers, tags, scenes, prefabs, models, animations, editor tools, packages, code style.
5. **[status-roadmap.md](status-roadmap.md)** — ✅ working / 🐞 buggy / 🚧 planned. **Check before building on a mechanic.**
6. **[hot-reload-remote.md](hot-reload-remote.md)** — trigger a live patch / full recompile in the running editor from outside (with the edit-depth rubric).

## Maintaining these docs
- These were written by reading every script under `Assets/Scripts/` + loose root scripts, the
  ScriptableObjects, editor tools, project settings, and confirming **design intent** with the
  developer (intent is largely not in the code).
- When you add/rename a system or change a cross-system contract, update the relevant system doc
  **and** the tables in [architecture.md](architecture.md) / [reference.md](reference.md).
- Keep status accurate: when a 🚧/🐞 item lands, move it to ✅ in [status-roadmap.md](status-roadmap.md).
- Don't document third-party folders (`AmplifyShaderEditor/`, `Plugins/`, `ConsolePro/`, `TextMesh Pro/`).
