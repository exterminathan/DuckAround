// TuningReapplyHooks.cs — live re-apply for fields that gameplay caches at Awake.
// The hub calls this right after a play-mode write; hooks push the fresh field
// value through the public re-apply path the gameplay script already exposes,
// WITHOUT editing any gameplay code. Everything is best-effort and try/caught —
// a broken hook must never break the tuning UI.
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TuningReapplyHooks {
	public static void AfterWrite(Object[] targets, string fieldName) {
		if (!EditorApplication.isPlaying || targets == null) return;
		foreach (var t in targets) {
			if (t == null) continue;
			try { Dispatch(t, fieldName); }
			catch (System.Exception e) {
				Debug.LogWarning($"[TuningHub] Re-apply hook failed for {t.name}.{fieldName}: {e.Message}");
			}
		}
	}

	static void Dispatch(Object target, string fieldName) {
		switch (target) {
			// WorkerAIController mirrors these into its BT blackboard in Awake();
			// SetStateAtValue is its public live write path.
			case WorkerAIController w:
				switch (fieldName) {
					case "MoveSpeed": w.SetStateAtValue("Speed", w.MoveSpeed); break;
					case "ArriveThreshold": w.SetStateAtValue("ArriveThreshold", w.ArriveThreshold); break;
					case "IsAllowedToMove": w.SetStateAtValue("IsAllowedToMove", w.IsAllowedToMove); break;
				}
				break;
		}
	}
}
