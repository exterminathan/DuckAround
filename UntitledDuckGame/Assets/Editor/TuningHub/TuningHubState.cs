// TuningHubState.cs — thin typed wrapper over SessionState (survives domain reloads,
// dies with the editor session) and EditorPrefs (persistent per machine).
// EditorPrefs is global across all Unity projects, so keys are project-prefixed.
using UnityEditor;
using UnityEngine.UIElements;

public static class TuningHubState {
	static string P => "TuningHub." + PlayerSettings.productName + ".";

	// ---- per-session (survives domain reload / hot-reload recompile)
	public static string ActiveCategory {
		get => SessionState.GetString(P + "category", "");
		set => SessionState.SetString(P + "category", value);
	}
	public static string SearchText {
		get => SessionState.GetString(P + "search", "");
		set => SessionState.SetString(P + "search", value);
	}

	// ---- persistent preferences
	public static bool SelectionMode {
		get => EditorPrefs.GetBool(P + "selectionMode", false);
		set => EditorPrefs.SetBool(P + "selectionMode", value);
	}
	public static bool WriteToScene {
		get => EditorPrefs.GetBool(P + "writeToScene", false);
		set => EditorPrefs.SetBool(P + "writeToScene", value);
	}
	public static bool ShowDead {
		get => EditorPrefs.GetBool(P + "showDead", false);
		set => EditorPrefs.SetBool(P + "showDead", value);
	}
	public static bool ShowUncategorized {
		get => EditorPrefs.GetBool(P + "showUncategorized", true);
		set => EditorPrefs.SetBool(P + "showUncategorized", value);
	}
	public static bool ModifiedOnly {
		get => EditorPrefs.GetBool(P + "modifiedOnly", false);
		set => EditorPrefs.SetBool(P + "modifiedOnly", value);
	}
	public static bool CaptureInspectorEdits {
		get => EditorPrefs.GetBool(P + "captureInspectorEdits", true);
		set => EditorPrefs.SetBool(P + "captureInspectorEdits", value);
	}

	// 0 = compact, 1 = medium (default), 2 = large — tier classes in TuningHubWindow.uss
	public static int UiScale {
		get => EditorPrefs.GetInt(P + "uiScale", 1);
		set => EditorPrefs.SetInt(P + "uiScale", value);
	}

	// Stamps the scale tier on a hub-styled root (main window + review window).
	public static void ApplyUiScaleClass(VisualElement root) {
		int s = UiScale;
		root.EnableInClassList("th-scale-md", s == 1);
		root.EnableInClassList("th-scale-lg", s == 2);
	}
}
