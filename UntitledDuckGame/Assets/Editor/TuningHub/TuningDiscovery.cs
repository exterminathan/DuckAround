// TuningDiscovery.cs — reflects over a component's serialized properties to find
// the tunable value fields (numbers, bools, vectors, colors, enums, curves, masks).
// The window diffs this against TuningManifest to build the "Other" bucket, so a
// newly added field always shows up somewhere instead of being silently hidden.
using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

public static class TuningDiscovery {
	static readonly HashSet<SerializedPropertyType> TunableTypes = new() {
		SerializedPropertyType.Float,
		SerializedPropertyType.Integer,
		SerializedPropertyType.Boolean,
		SerializedPropertyType.Enum,
		SerializedPropertyType.Color,
		SerializedPropertyType.Vector2,
		SerializedPropertyType.Vector3,
		SerializedPropertyType.Vector4,
		SerializedPropertyType.LayerMask,
		SerializedPropertyType.AnimationCurve,
		SerializedPropertyType.Gradient,
	};

	static readonly Dictionary<Type, List<string>> rootCache = new();

	// Root-level tunable serialized property names on this instance's type.
	public static List<string> TunableRoots(Object instance) {
		var type = instance.GetType();
		if (rootCache.TryGetValue(type, out var cached)) return cached;

		var roots = new List<string>();
		using (var so = new SerializedObject(instance)) {
			var it = so.GetIterator();
			bool enter = true;
			while (it.NextVisible(enter)) {
				enter = false; // roots only
				if (it.propertyPath == "m_Script") continue;
				if (TunableTypes.Contains(it.propertyType)) roots.Add(it.propertyPath);
			}
		}
		rootCache[type] = roots;
		return roots;
	}

	// Capture filter: is this leaf path rooted in a tunable field of the target?
	public static bool IsTunableRoot(Object instance, string rootName) {
		return TunableRoots(instance).Contains(rootName);
	}

	public static void ClearCache() => rootCache.Clear();
}
