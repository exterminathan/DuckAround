// TuningSnapshot.cs — the play-mode capture asset. Lives inside Assets/Editor/ so
// it never ships in a build. One entry per (object, leaf property) changed during
// play; values are stored in Unity's own PropertyModification string format.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public class TuningSnapshotEntry {
	public int instanceId;        // session-local coalescing key (survives domain reload, not editor restart)
	public string targetId;       // GlobalObjectId string ("" for runtime-spawned clones)
	public string rootName;       // clone-stripped root GameObject name — prefab-redirect fallback
	public string componentType;  // full type name
	public string displayName;    // "wheely_cart (ConveyorObjectMover)"
	public string propertyPath;   // leaf path, e.g. "gripOffset.x"
	public string baselineValue;  // value when play-mode editing began
	public string newValue;       // value to keep
	public bool include = true;   // review-window checkbox
}

// JsonUtility wrapper for the SessionState mirror (crash/domain-reload insurance).
[Serializable]
public class TuningSnapshotData {
	public List<TuningSnapshotEntry> entries = new();
}

public class TuningSnapshot : ScriptableObject {
	public List<TuningSnapshotEntry> entries = new();

	public const string AssetPath = "Assets/Editor/TuningHub/PlayModeSnapshot.asset";

	public static TuningSnapshot GetOrCreate() {
		var snap = AssetDatabase.LoadAssetAtPath<TuningSnapshot>(AssetPath);
		if (snap == null) {
			snap = CreateInstance<TuningSnapshot>();
			AssetDatabase.CreateAsset(snap, AssetPath);
		}
		return snap;
	}

	public void Save() {
		EditorUtility.SetDirty(this);
		AssetDatabase.SaveAssetIfDirty(this);
	}
}
