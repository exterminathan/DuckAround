# Hot Reload remote trigger

A small editor helper, [Assets/Editor/HotReloadRemote.cs](../../Assets/Editor/HotReloadRemote.cs),
lets an external process (e.g. an agent finishing a batch of edits) ask the **running** Unity editor to
do a Hot Reload **live patch** or a **full recompile** — so the right action is queued automatically at
the end of edits instead of relying on a manual UI click.

## Why a file (not `-executeMethod`)
`Unity -executeMethod` spawns a **new batch instance**; it can't command the editor you already have
open. So the helper polls a **trigger file** on `EditorApplication.update` (every ~0.75s) and acts on it.

## How to trigger it
Write one token to `<project>/Library/hotReloadRemoteRequest.txt` (the file is consumed/deleted by the
watcher). `Library/` is gitignored, so this never gets committed.

```bash
# full recompile (deep edits)
echo recompile > Library/hotReloadRemoteRequest.txt
# live patch (shallow edits)
echo reload    > Library/hotReloadRemoteRequest.txt
```
```powershell
Set-Content -Path Library/hotReloadRemoteRequest.txt -Value recompile -Encoding ascii
```

Also available manually: **Tools ▸ Hot Reload Remote ▸ Request Reload / Request Recompile**, and via
batch: `Unity -batchmode -quit -executeMethod HotReloadRemote.RequestRecompile`.

## What each token does (reflection into Hot Reload; graceful fallback)
- **`recompile`** → `HotReloadRunTab.Recompile()` (resets HR's compile suppression → `AssetDatabase.Refresh()`
  → `CompilationPipeline.RequestScriptCompilation()`; **exits play mode**). Fallbacks: the package menu
  item `Window/Hot Reload/Recompile`, then Unity's own `RequestScriptCompilation`.
- **`reload`** → `RequestHelper.RequestCompile(HotReloadState.CompileSessionId, …)` to force the HR
  server to rebuild patches now. (Body-only edits are auto-patched on save anyway; this just forces it.)

## Edit-depth rubric — which token the agent should queue
Refined rule (per the developer): a full recompile is needed **only when a brand-new function (method)
is created and then called**. Everything else hot-reloads with a `reload`.

| Use **`recompile`** (full) when… | Use **`reload`** (live patch) when… |
| --- | --- |
| a **new method** is added **and invoked** | edits are confined to existing method bodies |
| a new script/MonoBehaviour is added and used | a field **value** is changed (not declared) |
| **a new field is added** (HR can't add fields live — it rejects the patch) | a method signature is tweaked with no brand-new method being called |

⚠️ Adding a new field is the sneaky one: HR silently **rejects** a body patch that references a newly
added field and keeps running the OLD code, so the change looks like it did nothing. Always `recompile`
after adding a field. When unsure → **`recompile`** (safe). See
[[hot-reload-recompile-workflow]] in memory for the
underlying why (Hot Reload won't pick up structural changes with a live patch, and can get stuck on a
stale error if it compiles a half-applied multi-edit change).

## First-run note
`HotReloadRemote.cs` is itself a script — the watcher only starts running **after** the editor has
compiled it once. So the very first time, trigger a normal full recompile (focus Unity / the menu item).
After that the trigger file works for subsequent batches.
