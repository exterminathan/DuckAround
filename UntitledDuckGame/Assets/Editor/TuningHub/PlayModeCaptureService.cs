// PlayModeCaptureService.cs — records every tuning-field edit made during play mode
// (hub OR normal Inspector — both go through the Undo pipeline), mirrors the record
// to SessionState so a mid-play domain reload can't lose it, saves it to the
// TuningSnapshot asset on play exit, and prompts Apply / Review… / Discard back in
// edit mode. Apply writes the values onto the real scene objects / prefab assets.
//
// Identity across the play-mode boundary: GlobalObjectId for objects that exist in
// the saved scene; runtime-spawned clones (no persistent id) redirect to the prefab
// asset whose root name matches the clone's root ("(Clone)" stripped).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayModeCaptureService {
	const string MirrorKey = "TuningHub.SnapshotMirror";
	const string PromptPendingKey = "TuningHub.PromptPending";
	const double MirrorInterval = 2.0;

	static readonly Dictionary<string, TuningSnapshotEntry> live = new();
	static bool mirrorDirty;
	static double nextMirror;

	public static event Action Changed;
	public static int PendingCount => live.Count;

	static PlayModeCaptureService() {
		Undo.postprocessModifications += OnPostprocessModifications;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		EditorApplication.update += OnUpdate;

		// domain reload mid-play (script recompile while playing): restore the record
		if (EditorApplication.isPlayingOrWillChangePlaymode) LoadFromMirror();
	}

	// ------------------------------------------------------------------ recording

	static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] mods) {
		if (!EditorApplication.isPlayingOrWillChangePlaymode) return mods;

		foreach (var mod in mods) {
			var cur = mod.currentValue;
			if (cur == null || cur.target == null) continue;
			var target = cur.target;

			// asset edits (SOs, prefab assets) persist through play exit on their own
			if (EditorUtility.IsPersistent(target)) continue;
			if (cur.objectReference != null || mod.previousValue?.objectReference != null) continue;

			var type = target.GetType();
			if (!TuningManifest.CoversInstanceType(type)) continue;

			string root = RootOf(cur.propertyPath);
			if (!TuningManifest.CoveredRootsForInstance(type).Contains(root)
				&& !TuningDiscovery.IsTunableRoot(target, root)) continue;

			Record(target, cur.propertyPath, mod.previousValue?.value, cur.value);
		}
		return mods;
	}

	static void Record(Object target, string propertyPath, string previous, string current) {
		string key = target.GetInstanceID() + "|" + propertyPath;
		if (live.TryGetValue(key, out var entry)) {
			entry.newValue = current; // keep the FIRST baseline, update the latest value
		}
		else {
			var id = GlobalObjectId.GetGlobalObjectIdSlow(target);
			string rootName = "";
			if (target is Component c) rootName = c.transform.root.name.Replace("(Clone)", "").Trim();
			live[key] = new TuningSnapshotEntry {
				instanceId = target.GetInstanceID(),
				targetId = id.identifierType == 0 ? "" : id.ToString(),
				rootName = rootName,
				componentType = target.GetType().FullName,
				displayName = $"{target.name} ({target.GetType().Name})",
				propertyPath = propertyPath,
				baselineValue = previous ?? "",
				newValue = current,
			};
		}
		mirrorDirty = true;
		Changed?.Invoke();
	}

	// Row-dot query: does this target have a pending play-mode entry under this root field?
	public static bool HasPendingFor(Object[] targets, string rootPath) {
		if (live.Count == 0 || targets == null) return false;
		foreach (var t in targets) {
			if (t == null) continue;
			int id = t.GetInstanceID();
			foreach (var e in live.Values)
				if (e.instanceId == id && RootOf(e.propertyPath) == rootPath) return true;
		}
		return false;
	}

	// ------------------------------------------------------- play-mode lifecycle

	static void OnUpdate() {
		if (!mirrorDirty || EditorApplication.timeSinceStartup < nextMirror) return;
		nextMirror = EditorApplication.timeSinceStartup + MirrorInterval;
		mirrorDirty = false;
		SessionState.SetString(MirrorKey, JsonUtility.ToJson(new TuningSnapshotData { entries = live.Values.ToList() }));
	}

	static void OnPlayModeStateChanged(PlayModeStateChange change) {
		switch (change) {
			case PlayModeStateChange.EnteredPlayMode:
				live.Clear();
				SessionState.EraseString(MirrorKey);
				Changed?.Invoke();
				break;

			case PlayModeStateChange.ExitingPlayMode: {
				// persist BEFORE the exit domain reload wipes statics
				var snap = TuningSnapshot.GetOrCreate();
				snap.entries = live.Values.ToList();
				snap.Save();
				SessionState.SetBool(PromptPendingKey, snap.entries.Count > 0);
				SessionState.EraseString(MirrorKey);
				break;
			}

			case PlayModeStateChange.EnteredEditMode:
				if (SessionState.GetBool(PromptPendingKey, false))
					EditorApplication.delayCall += Prompt;
				break;
		}
	}

	static void LoadFromMirror() {
		string json = SessionState.GetString(MirrorKey, "");
		if (string.IsNullOrEmpty(json)) return;
		try {
			var data = JsonUtility.FromJson<TuningSnapshotData>(json);
			if (data?.entries == null) return;
			foreach (var e in data.entries)
				live[e.instanceId + "|" + e.propertyPath] = e;
		}
		catch (Exception ex) {
			Debug.LogWarning("[TuningHub] Could not restore play-mode capture mirror: " + ex.Message);
		}
	}

	// ------------------------------------------------------------ prompt & apply

	static void Prompt() {
		SessionState.EraseBool(PromptPendingKey);
		var snap = TuningSnapshot.GetOrCreate();
		if (snap.entries.Count == 0) return;

		int choice = EditorUtility.DisplayDialogComplex("Tuning Hub",
			$"{snap.entries.Count} tuning value(s) were changed during play mode.\n\n" +
			"Apply them to the scene objects / prefabs so they stick?",
			"Apply", "Review…", "Discard");

		switch (choice) {
			case 0: ApplyEntries(snap, includedOnly: false); break;
			case 1: TuningSnapshotReviewWindow.Open(); break;
			case 2: Clear(snap); break;
		}
	}

	public static void ApplyEntries(TuningSnapshot snap, bool includedOnly) {
		Undo.IncrementCurrentGroup();
		int group = Undo.GetCurrentGroup();

		int applied = 0, redirected = 0;
		var failures = new List<TuningSnapshotEntry>();
		var soCache = new Dictionary<Object, SerializedObject>();

		foreach (var e in snap.entries) {
			if (includedOnly && !e.include) continue;

			var target = ResolveTarget(e, out bool redirect);
			if (target == null) { failures.Add(e); continue; }

			if (!soCache.TryGetValue(target, out var so))
				soCache[target] = so = new SerializedObject(target);

			var p = so.FindProperty(e.propertyPath);
			if (p == null || !TuningValueCodec.TrySet(p, e.newValue)) { failures.Add(e); continue; }
			applied++;
			if (redirect) redirected++;
		}

		foreach (var kv in soCache) {
			kv.Value.ApplyModifiedProperties(); // registers Undo + records prefab-instance overrides
			if (EditorUtility.IsPersistent(kv.Key)) {
				EditorUtility.SetDirty(kv.Key);
				AssetDatabase.SaveAssetIfDirty(kv.Key);
			}
			else if (kv.Key is Component c) {
				EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
			}
			kv.Value.Dispose();
		}

		Undo.SetCurrentGroupName("Apply Tuning Snapshot");
		Undo.CollapseUndoOperations(group);

		snap.entries = failures;
		snap.Save();

		string msg = $"[TuningHub] Snapshot applied: {applied} value(s)" +
			(redirected > 0 ? $", {redirected} redirected to prefab assets (runtime-spawned clones)" : "");
		if (failures.Count > 0)
			Debug.LogWarning(msg + $". {failures.Count} entr(ies) could not be resolved and were kept in the snapshot:\n  " +
				string.Join("\n  ", failures.Select(f => $"{f.displayName} · {f.propertyPath}")));
		else
			Debug.Log(msg + ".");

		Changed?.Invoke();
	}

	public static void Clear(TuningSnapshot snap) {
		snap.entries.Clear();
		snap.Save();
		Changed?.Invoke();
	}

	static Object ResolveTarget(TuningSnapshotEntry e, out bool redirected) {
		redirected = false;

		if (!string.IsNullOrEmpty(e.targetId) && GlobalObjectId.TryParse(e.targetId, out var id)) {
			var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
			if (obj != null) return obj;
		}

		var type = FindType(e.componentType);
		var prefabComp = TuningTargetResolver.FindPrefabComponentByRootName(e.rootName, type);
		if (prefabComp != null) { redirected = true; return prefabComp; }
		return null;
	}

	static Type FindType(string fullName) {
		if (string.IsNullOrEmpty(fullName)) return null;
		var t = Type.GetType(fullName);
		if (t != null) return t;
		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
			t = asm.GetType(fullName);
			if (t != null) return t;
		}
		return null;
	}

	static string RootOf(string path) {
		int i = path.IndexOf('.');
		return i < 0 ? path : path.Substring(0, i);
	}
}
