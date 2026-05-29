# Audio (FMOD)

Scripts: `Assets/Scripts/Audio/`. Uses **FMOD for Unity** (not Unity's built-in audio mixer).
FMOD banks live under `Assets/StreamingAssets/`. FMOD project log: `fmod_editor.log` (repo root).

## Pieces

- **`FMODEvents.cs`** — a scene singleton (`instance`) exposing serialized `EventReference`s:
  - `ragdollSound`
  - `collisionSound`
  Add new sounds here as `[field: SerializeField] public EventReference xxx { get; private set; }`.

- **`AudioManager.cs`** — scene singleton (`instance`). On `Awake` builds a **string → EventReference**
  map from `FMODEvents`:
  ```
  "ragdoll"          → events.ragdollSound
  "playerHitWorker"  → events.collisionSound
  ```
  `Get(key)` returns the `EventReference` for a key. To add a sound: add the field in `FMODEvents`,
  then register a key here.

- **`AudioAgent.cs`** — put on any actor that plays sound (player, each worker). On `Awake` it
  caches the `StudioEventEmitter[]` components on the same GameObject. `Play(key)` looks up the
  `EventReference` via `AudioManager.instance.Get(key)` and calls `PlayEvent`, which finds the
  emitter whose `EventReference.Guid` matches and plays it. (If no emitter matches, it currently
  no-ops — emitters are expected to be pre-placed; dynamic assignment is a noted TODO.)

## Call sites
- `WorkerAIController.ApplyRagdoll(true)` and `OnCollisionEnter` → `audioAgent.Play("ragdoll")`.
- `ArmHitForwarder` (player arm hits a Worker) → `worker.audioAgent.Play("playerHitWorker")`.

## Adding a new sound (recipe)
1. Add an `EventReference` field to `FMODEvents.cs` and assign the FMOD event in the inspector.
2. Register a `"key"` → field entry in `AudioManager.Awake`'s `eventMap`.
3. Add a matching `StudioEventEmitter` (with that event) to the actor that should play it.
4. Call `audioAgent.Play("key")` from gameplay code.

## Gotchas
- `AudioAgent.PlayEvent` matches emitters by **GUID**; an actor must already have a
  `StudioEventEmitter` for that event or nothing plays.
- Both `AudioManager` and `FMODEvents` are singletons — exactly one of each per scene.
