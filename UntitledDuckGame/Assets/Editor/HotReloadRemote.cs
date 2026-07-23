// HotReloadRemote.cs
// Lets an external process (e.g. an AI agent finishing a batch of edits) request a Hot Reload action
// against the *running* Unity editor by writing a token to a trigger file that this watcher polls.
//
// Why a file: `Unity -executeMethod` spawns a NEW batch instance; it can't command the already-running
// editor. A file polled on EditorApplication.update is the reliable signal channel into the live editor.
//
// Trigger file: <project>/Library/hotReloadRemoteRequest.txt  (Library is gitignored)
// Tokens (case-insensitive, file content):
//   reload     -> force a live Hot Reload patch          (use for method-BODY-only edits)
//   recompile  -> full domain recompile (exits play mode) (use for new files / new or renamed members /
//                                                          signature, type, enum, field or attribute changes)
//
// Also provides Tools/Hot Reload Remote/* menu items and public static methods callable via
//   Unity -batchmode -quit -executeMethod HotReloadRemote.RequestRecompile
//
// All Hot Reload calls go through reflection because its asmdef is autoReferenced:false. If the package
// is missing/renamed, recompile falls back to Unity's own compile pipeline and reload no-ops gracefully.

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class HotReloadRemote {
    const string TriggerFileName = "hotReloadRemoteRequest.txt";
    const double PollIntervalSeconds = 0.75;

    static readonly string TriggerFilePath =
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", TriggerFileName));

    static double _nextPoll;

    static HotReloadRemote() {
        EditorApplication.update += PollTriggerFile;
    }

    static void PollTriggerFile() {
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;

        string token;
        try {
            if (!File.Exists(TriggerFilePath)) return;
            token = File.ReadAllText(TriggerFilePath).Trim().ToLowerInvariant();
            File.Delete(TriggerFilePath); // consume before acting (recompile triggers a domain reload)
        }
        catch (Exception e) {
            Debug.LogWarning($"[HotReloadRemote] Could not read/clear trigger file: {e.Message}");
            return;
        }

        if (string.IsNullOrEmpty(token)) return;

        switch (token) {
            case "recompile": RequestRecompile(); break;
            case "reload": RequestReload(); break;
            default:
                Debug.LogWarning($"[HotReloadRemote] Unknown token '{token}' (expected 'reload' or 'recompile').");
                break;
        }
    }

    /// <summary>
    /// Force a live Hot Reload patch now. Body-only edits are auto-patched by HR when files are saved;
    /// this just asks the server to (re)build patches for the current files immediately.
    /// </summary>
    [MenuItem("Tools/Hot Reload Remote/Request Reload (live patch)")]
    public static void RequestReload() {
        try {
            var requestHelper = FindType("SingularityGroup.HotReload.RequestHelper");
            var stateType = FindType("SingularityGroup.HotReload.Editor.HotReloadState");
            if (requestHelper == null || stateType == null) {
                Debug.Log("[HotReloadRemote] Reload: Hot Reload not found; relying on its auto-patch on save.");
                return;
            }

            var sessionProp = stateType.GetProperty("CompileSessionId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var sessionId = (string)(sessionProp?.GetValue(null) ?? string.Empty);

            var method = requestHelper.GetMethod("RequestCompile", BindingFlags.Public | BindingFlags.Static);
            if (method == null) {
                Debug.Log("[HotReloadRemote] Reload: RequestCompile not found; relying on HR auto-patch on save.");
                return;
            }

            // RequestCompile(string sessionId, Action<string> onAsset). HR reports changed scene paths; no-op.
            Action<string> onAsset = _ => { };
            method.Invoke(null, new object[] { sessionId, onAsset });
            Debug.Log("[HotReloadRemote] Reload: requested live patch from Hot Reload server.");
        }
        catch (Exception e) {
            Debug.LogWarning($"[HotReloadRemote] Reload request failed ({e.Message}); HR auto-patches on save anyway.");
        }
    }

    /// <summary>
    /// Full domain recompile (needed for new files / new or renamed members / signature, type, enum,
    /// field, or attribute changes). Mirrors the Hot Reload "Recompile" button (exits play mode).
    /// </summary>
    [MenuItem("Tools/Hot Reload Remote/Request Recompile (full)")]
    public static void RequestRecompile() {
        // Drop any live Inspector targets before the domain reload lands. An externally
        // triggered recompile can otherwise strand Inspector/Properties windows holding
        // dead targets (SerializedObjectNotCreatableException / MissingReferenceException
        // on every play-mode entry until the windows are rebuilt).
        try {
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
        catch { /* selection guard is best-effort; never block the recompile */ }

        // 1) Preferred: HotReloadRunTab.Recompile() — resets HR's compile suppression, refreshes, requests compile.
        try {
            var runTab = FindType("SingularityGroup.HotReload.Editor.HotReloadRunTab");
            var recompile = runTab?.GetMethod("Recompile",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (recompile != null) {
                recompile.Invoke(null, null);
                Debug.Log("[HotReloadRemote] Recompile: invoked HotReloadRunTab.Recompile().");
                return;
            }
        }
        catch (Exception e) {
            Debug.LogWarning($"[HotReloadRemote] HotReloadRunTab.Recompile() failed ({e.Message}); trying fallbacks.");
        }

        // 2) Fallback: the package's own menu item.
        try {
            if (EditorApplication.ExecuteMenuItem("Window/Hot Reload/Recompile")) {
                Debug.Log("[HotReloadRemote] Recompile: triggered via Hot Reload menu item.");
                return;
            }
        }
        catch { /* fall through */ }

        // 3) Last resort: Unity's own pipeline (works even without Hot Reload).
        AssetDatabase.Refresh();
        if (!EditorApplication.isCompiling) {
            CompilationPipeline.RequestScriptCompilation();
        }
        Debug.Log("[HotReloadRemote] Recompile: fell back to AssetDatabase.Refresh + RequestScriptCompilation.");
    }

    static Type FindType(string fullName) {
        var t = Type.GetType(fullName);
        if (t != null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
