// TuningManifestValidator.cs — catches manifest rot: field names in TuningManifest
// that no longer resolve against the real serialized layout (renamed/removed fields).
// Runs once per editor session when the hub opens; also on demand from the menu.
using System.Text;
using UnityEditor;
using Object = UnityEngine.Object;

public static class TuningManifestValidator {
	const string RanKey = "TuningHub.ValidatorRan";

	public static void RunOnce() {
		if (SessionState.GetBool(RanKey, false)) return;
		SessionState.SetBool(RanKey, true);
		Validate(logWhenClean: false);
	}

	[MenuItem("Tools/Tuning Hub/Validate Manifest")]
	public static void ValidateNow() => Validate(logWhenClean: true);

	static void Validate(bool logWhenClean) {
		var problems = new StringBuilder();

		foreach (var cat in TuningManifest.Categories) {
			foreach (var group in cat.groups) {
				var targets = TuningTargetResolver.Resolve(group.componentType);
				Object rep = targets.assetComponents.Length > 0 ? targets.assetComponents[0]
					: targets.sceneComponents.Length > 0 ? targets.sceneComponents[0] : null;
				if (rep == null) {
					problems.AppendLine($"  [{cat.name} / {group.title}] no instance of {group.componentType.Name} found in scenes or prefabs — fields unverified");
					continue;
				}

				using var so = new SerializedObject(rep);
				string prefix = null;
				if (group.arrayProperty != null) {
					var arr = so.FindProperty(group.arrayProperty);
					if (arr == null || !arr.isArray) {
						problems.AppendLine($"  [{cat.name} / {group.title}] array property '{group.arrayProperty}' missing on {group.componentType.Name}");
						continue;
					}
					if (arr.arraySize == 0) continue; // nothing to verify element fields against
					prefix = $"{group.arrayProperty}.Array.data[0].";
				}

				foreach (var field in group.fields) {
					string path = prefix != null ? prefix + field.fieldName : field.fieldName;
					if (so.FindProperty(path) == null)
						problems.AppendLine($"  [{cat.name} / {group.title}] '{field.fieldName}' not found on {group.componentType.Name}");
				}
			}
		}

		if (problems.Length > 0)
			UnityEngine.Debug.LogWarning("[TuningHub] Manifest validation found stale entries:\n" + problems);
		else if (logWhenClean)
			UnityEngine.Debug.Log("[TuningHub] Manifest validation: all fields resolve.");
	}
}
