# Global Alarm — facility alert level (L0–L7)

Scripts: `Assets/Scripts/GlobalState/` + `Assets/Scriptable Objects/AlarmLevelData.cs`.
Asset: `Assets/Scriptable Objects/AlarmLevelDataStandard.asset`.

## What it represents
A single facility-wide **alert level** that captures how alarmed/aggressive the Workers are.
Higher level → Workers detect the player **farther and through a wider cone** and **chase faster**.
It is the game's escalating pressure meter.

```
enum AlarmLevel { L0, L1, L2, L3, L4, L5, L6, L7 }   // 8 levels (index 0–7)
```

## GlobalAlarm.cs (singleton MonoBehaviour)
- `public static AlarmLevel Level { get; private set; }` — current level (starts `L0`).
- Internally tracks a float `currentValue` clamped to **0–7**; `Level = floor(currentValue)`.
- `static RequestIncrease(float amount)` — raises `currentValue` (clamped 0–7), **gated by a
  cooldown** (`cooldownDuration`, default 7.5s) so rapid re-detections don't spike it. Called by
  `DetectionActions.BeginChase(... RequestIncrease(1f))`.
- Serialized `decayRate` exists but **decay is not currently applied** in `Update()` (so the alarm
  does not auto-fall yet — see roadmap; planned reset is via levers).
- Static accessors used elsewhere:
  - `GetCurrentLevelData()` → `AlarmConfig.AlarmLevelData` for the current level.
  - `GetDefaultLevelData()` → data for `L0`.
  - `GetMaxAlarmLevelData()` → data for `L7` (used by `OuterCircleSpriteManager`).
  - `LevelToString()` → the numeric part (e.g. `"3"`).

## AlarmConfig (ScriptableObject, in AlarmLevelData.cs)
`[CreateAssetMenu(menuName = "Scriptable Objects/AlarmLevel")]`. Holds an array of per-level data:

```csharp
struct AlarmLevelData {
    AlarmLevel Level;
    float playerDetectionAngle;     // vision cone width
    float playerDetectionDistance;  // vision range
    float chaseTimer;               // how long a worker keeps chasing after losing sight
    float playerChaseSpeed;         // chase speed multiplier
}
```
`GetLevelData(level)` clamps the index into the array. The configured asset is
**`AlarmLevelDataStandard.asset`** — tune balance there, not in code.

## AlarmManager.cs (HUD)
Watches `GlobalAlarm.Level`; when it changes, updates a `TMP_Text` with `LevelToString()`.
Pure display.

## Who reads the alarm
- `DetectionChecks.DetectPlayer` / `DetectionActions.BeginChase` — use `playerDetectionAngle` /
  `playerDetectionDistance`.
- `DetectionActions.ChasePlayerTick` — uses `playerChaseSpeed` (and sets animator speed).
- `DetectionChecks.LostPlayer` — uses `chaseTimer`.
- `WorkerVisualController` — visual cone angle/range; `OuterCircleSpriteManager` — ring thickness.

## Gotchas / status
- **No auto-decay** is wired (despite `decayRate`). The alarm currently only goes up.
- **Levers do not reset/lower the alarm yet** — that hookup is planned and should route through
  the general `IInteractable` system. See [status-roadmap.md](status-roadmap.md).
- `GlobalAlarm` is a singleton via `instance`; ensure exactly one exists per scene.
