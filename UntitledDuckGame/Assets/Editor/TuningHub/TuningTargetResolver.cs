// TuningTargetResolver.cs — resolves a tunable component type to the objects the
// hub can edit: components on prefab ASSETS, components in loaded scenes (incl. an
// open prefab stage), or ScriptableObject assets.
//
// The prefab sweep matters: several tunable types (HeldItemController,
// ConveyorObjectMover) live ONLY on prefab assets and have zero scene instances,
// so FindObjectsByType alone would show nothing for them.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class TuningTargets {
	public Object[] assetComponents = Array.Empty<Object>();  // on prefab assets (or SO assets)
	public Object[] sceneComponents = Array.Empty<Object>();  // in loaded scenes / prefab stage
	public bool isScriptableObject;
}

public static class TuningTargetResolver {
	static readonly string[] SkipPathFragments = {
		"/Plugins/", "/AmplifyShaderEditor/", "/ConsolePro/", "/TextMesh Pro/", "/StreamingAssets/",
	};

	static Dictionary<Type, TuningTargets> cache;
	static List<GameObject> prefabRoots; // loaded prefab asset roots (filtered sweep, cached)

	public static void Invalidate() {
		cache = null;
		prefabRoots = null;
	}

	public static TuningTargets Resolve(Type type) {
		cache ??= new Dictionary<Type, TuningTargets>();
		if (cache.TryGetValue(type, out var hit)) return hit;

		var result = new TuningTargets();

		if (typeof(ScriptableObject).IsAssignableFrom(type)) {
			result.isScriptableObject = true;
			result.assetComponents = AssetDatabase.FindAssets("t:" + type.Name)
				.Select(g => AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(g)))
				.Where(o => o != null && o.GetType() == type)
				.ToArray();
		}
		else {
			// prefab assets (exact type — subclasses have their own serialized layout)
			var assets = new List<Object>();
			foreach (var root in PrefabRoots())
				foreach (var c in root.GetComponentsInChildren(type, true))
					if (c.GetType() == type) assets.Add(c);
			result.assetComponents = assets.ToArray();

			// loaded scenes + open prefab stage
			var scene = new List<Object>();
			foreach (var c in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
				if (c.GetType() == type) scene.Add(c);
			var stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.prefabContentsRoot != null)
				foreach (var c in stage.prefabContentsRoot.GetComponentsInChildren(type, true))
					if (c.GetType() == type && !scene.Contains(c)) scene.Add(c);
			result.sceneComponents = scene.ToArray();
		}

		cache[type] = result;
		return result;
	}

	// Snapshot fallback: a runtime-spawned clone has no persistent GlobalObjectId, so
	// entries redirect to the prefab asset whose root name matches the clone's root.
	public static Object FindPrefabComponentByRootName(string rootName, Type type) {
		if (string.IsNullOrEmpty(rootName) || type == null) return null;
		foreach (var root in PrefabRoots()) {
			if (!string.Equals(root.name, rootName, StringComparison.OrdinalIgnoreCase)) continue;
			var comps = root.GetComponentsInChildren(type, true).Where(c => c.GetType() == type).ToArray();
			if (comps.Length > 0) return comps[0];
		}
		return null;
	}

	static List<GameObject> PrefabRoots() {
		if (prefabRoots != null) return prefabRoots;
		prefabRoots = new List<GameObject>();
		foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) {
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (SkipPathFragments.Any(path.Contains)) continue;
			var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (go != null) prefabRoots.Add(go);
		}
		return prefabRoots;
	}
}
