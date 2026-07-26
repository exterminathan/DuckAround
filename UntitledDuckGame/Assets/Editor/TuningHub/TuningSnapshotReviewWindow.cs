// TuningSnapshotReviewWindow.cs — the "Review…" branch of the exit-play prompt:
// a checkable list of captured play-mode changes (target · field · was → now),
// with Apply Selected / Discard All.
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class TuningSnapshotReviewWindow : EditorWindow {
	ListView list;
	Label summary;

	public static void Open() {
		var w = GetWindow<TuningSnapshotReviewWindow>(true, "Tuning Snapshot Review", true);
		w.minSize = new Vector2(520, 260);
	}

	void CreateGUI() {
		var root = rootVisualElement;
		var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/TuningHub/TuningHubWindow.uss");
		if (uss != null) root.styleSheets.Add(uss);
		root.AddToClassList("th-root");
		root.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
		root.AddToClassList("th-review");
		TuningHubState.ApplyUiScaleClass(root);

		var snap = TuningSnapshot.GetOrCreate();

		summary = new Label();
		summary.AddToClassList("th-review-summary");
		root.Add(summary);

		list = new ListView {
			fixedItemHeight = 24,
			itemsSource = snap.entries,
			selectionType = SelectionType.None,
			makeItem = MakeItem,
			bindItem = (el, i) => BindItem(el, snap, i),
		};
		list.style.flexGrow = 1;
		root.Add(list);

		var buttons = new VisualElement();
		buttons.AddToClassList("th-review-buttons");

		var applyBtn = new Button(() => {
			PlayModeCaptureService.ApplyEntries(snap, includedOnly: true);
			Refresh(snap);
		}) { text = "Apply Selected" };
		applyBtn.AddToClassList("th-btn-primary");
		buttons.Add(applyBtn);

		buttons.Add(new Button(() => {
			PlayModeCaptureService.Clear(snap);
			Refresh(snap);
		}) { text = "Discard All" });

		buttons.Add(new Button(Close) { text = "Close" });
		root.Add(buttons);

		Refresh(snap);
	}

	VisualElement MakeItem() {
		var row = new VisualElement();
		row.AddToClassList("th-review-row");

		var toggle = new Toggle { name = "include" };
		// register ONCE here (bindItem runs on recycled rows — registering there stacks callbacks)
		toggle.RegisterValueChangedCallback(evt => {
			if (row.userData is TuningSnapshotEntry entry) {
				entry.include = evt.newValue;
				EditorUtility.SetDirty(TuningSnapshot.GetOrCreate());
			}
		});
		row.Add(toggle);

		var target = new Label { name = "target" };
		target.AddToClassList("th-review-target");
		row.Add(target);

		var field = new Label { name = "field" };
		field.AddToClassList("th-review-field");
		row.Add(field);

		var change = new Label { name = "change" };
		change.AddToClassList("th-review-change");
		row.Add(change);

		var status = new Label { name = "status" };
		status.AddToClassList("th-review-status");
		row.Add(status);
		return row;
	}

	void BindItem(VisualElement el, TuningSnapshot snap, int i) {
		if (i < 0 || i >= snap.entries.Count) return;
		var e = snap.entries[i];
		el.userData = e;

		el.Q<Toggle>("include").SetValueWithoutNotify(e.include);
		el.Q<Label>("target").text = e.displayName;
		el.Q<Label>("field").text = e.propertyPath;
		el.Q<Label>("change").text = $"{Short(e.baselineValue)} → {Short(e.newValue)}";

		bool resolvable = !string.IsNullOrEmpty(e.targetId);
		var status = el.Q<Label>("status");
		status.text = resolvable ? "" : "→ prefab";
		status.tooltip = resolvable ? "" :
			$"Runtime-spawned clone — will be applied to the '{e.rootName}' prefab asset instead.";
	}

	static string Short(string v) => string.IsNullOrEmpty(v) ? "—" : (v.Length > 12 ? v.Substring(0, 12) + "…" : v);

	void Refresh(TuningSnapshot snap) {
		summary.text = snap.entries.Count == 0
			? "No pending play-mode changes."
			: $"{snap.entries.Count} value(s) captured during play mode. Unchecked entries are discarded on Apply.";
		list.itemsSource = snap.entries;
		list.Rebuild();
	}
}
