// TuningFieldRow.cs — one tuning knob: modified dot · label · control · unit ·
// apply badge · scope pill. Owns its binding lifecycle; the scope pill can rebind
// the row from "all instances" to a single selected object (a prefab override).
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class TuningRowContext {
	public Object[] sceneInstances = Array.Empty<Object>(); // for the override menu
	public bool bucketIsAssets;
	public string bucketLabel = "Scene";
	public bool allowScopeMenu = true;
	public Action<SerializedObject> registerSO;             // window tracks SOs for flush/dispose
}

public class TuningFieldRow : VisualElement {
	public readonly TuningFieldSpec Spec;
	public string Haystack { get; private set; }

	readonly TuningRowContext ctx;
	readonly SerializedProperty sharedProp;
	readonly string path;
	readonly string rootPath;

	SerializedProperty currentProp;
	VisualElement dot;
	VisualElement control;
	Button scopePill;
	Object scopeTarget; // null = all

	object baseline;
	bool baselineValid;
	int dragUndoGroup = -1;

	public bool IsModified { get; private set; }

	public TuningFieldRow(TuningFieldSpec spec, SerializedProperty prop, TuningRowContext context, string labelOverride = null) {
		Spec = spec;
		ctx = context;
		sharedProp = prop;
		currentProp = prop;
		path = prop.propertyPath;
		int dotIdx = path.IndexOf('.');
		rootPath = dotIdx < 0 ? path : path.Substring(0, dotIdx);

		AddToClassList("th-row");
		if (spec.dead) AddToClassList("th-row--dead");

		string label = labelOverride ?? spec.label;
		Haystack = (label + " " + path + " " + prop.serializedObject.targetObject.GetType().Name + " " + (spec.unit ?? "")).ToLowerInvariant();

		try {
			baseline = prop.boxedValue;
			baselineValid = baseline is float || baseline is int || baseline is bool || baseline is Enum
				|| baseline is UnityEngine.Vector2 || baseline is UnityEngine.Vector3 || baseline is UnityEngine.Vector4
				|| baseline is UnityEngine.Color || baseline is UnityEngine.LayerMask;
		}
		catch { baselineValid = false; }

		BuildUI(label);
	}

	void BuildUI(string label) {
		dot = new VisualElement();
		dot.AddToClassList("th-dot");
		Add(dot);

		var labelEl = new Label(label);
		labelEl.AddToClassList("th-label");
		labelEl.tooltip = BuildTooltip();
		Add(labelEl);

		control = TuningControlFactory.Create(Spec, currentProp);
		control.TrackPropertyValue(currentProp, OnPropChanged);
		Add(control);

		if (!string.IsNullOrEmpty(Spec.unit)) {
			var unit = new Label(Spec.unit);
			unit.AddToClassList("th-unit");
			Add(unit);
		}

		if (Spec.apply != TuningApplyMode.Live) {
			var badge = new Label(BadgeText());
			badge.AddToClassList("th-badge");
			badge.AddToClassList(Spec.apply switch {
				TuningApplyMode.Reapply => "is-auto",
				TuningApplyMode.NextEvent => "is-warn",
				TuningApplyMode.RestartPlay => "is-hard",
				_ => "is-edit",
			});
			badge.tooltip = BadgeTooltip();
			Add(badge);
		}

		if (ctx.allowScopeMenu) {
			scopePill = new Button(OpenScopeMenu);
			scopePill.AddToClassList("th-scope");
			UpdateScopePill();
			Add(scopePill);
		}

		// Collapse a whole slider drag into ONE undo step.
		RegisterCallback<PointerDownEvent>(_ => {
			Undo.IncrementCurrentGroup();
			dragUndoGroup = Undo.GetCurrentGroup();
		}, TrickleDown.TrickleDown);
		RegisterCallback<PointerUpEvent>(_ => {
			if (dragUndoGroup < 0) return;
			Undo.SetCurrentGroupName("Tune " + Spec.label);
			Undo.CollapseUndoOperations(dragUndoGroup);
			dragUndoGroup = -1;
		}, TrickleDown.TrickleDown);

		UpdateDot();
	}

	string BuildTooltip() {
		string t = Spec.tooltip;
		string field = "Field: " + path;
		if (Spec.dead) return $"DEAD FIELD — {Spec.deadReason}\n{field}";
		return string.IsNullOrEmpty(t) ? field : t + "\n" + field;
	}

	string BadgeText() => Spec.apply switch {
		TuningApplyMode.Reapply => "auto",
		TuningApplyMode.NextEvent => Spec.applyNote ?? "next use",
		TuningApplyMode.RestartPlay => "restart",
		TuningApplyMode.EditorOnly => "edit-time",
		_ => "",
	};

	string BadgeTooltip() => Spec.apply switch {
		TuningApplyMode.Reapply => "Cached at Awake — the hub re-applies this live when changed in play mode.",
		TuningApplyMode.NextEvent => $"Read when the action fires — a play-mode edit applies on the {Spec.applyNote ?? "next use"}.",
		TuningApplyMode.RestartPlay => "Consumed in Awake/Start with no re-apply path — restart play mode to apply.",
		TuningApplyMode.EditorOnly => "Only meaningful at edit time.",
		_ => "",
	};

	void OnPropChanged(SerializedProperty p) {
		UpdateDot();
		if (Spec.apply == TuningApplyMode.Reapply && EditorApplication.isPlaying)
			TuningReapplyHooks.AfterWrite(p.serializedObject.targetObjects, rootPath);
	}

	// dot priority: pending play-mode snapshot > changed this session > prefab override
	public void UpdateDot() {
		string cls = null, tip = null;
		try {
			if (currentProp.serializedObject.targetObject == null) return;

			if (PlayModeCaptureService.HasPendingFor(currentProp.serializedObject.targetObjects, rootPath)) {
				cls = "is-snapshot";
				tip = "Changed during play — pending in the exit-play snapshot.";
			}
			else if (baselineValid && !Equals(SafeBoxed(), baseline)) {
				cls = "is-session";
				tip = "Changed since this window was opened.";
			}
			else if (!ctx.bucketIsAssets && currentProp.prefabOverride) {
				cls = "is-override";
				tip = "Overrides the prefab value on this instance.";
			}
		}
		catch { /* stale SO mid-reload — the window rebuilds momentarily */ }

		dot.EnableInClassList("is-snapshot", cls == "is-snapshot");
		dot.EnableInClassList("is-session", cls == "is-session");
		dot.EnableInClassList("is-override", cls == "is-override");
		dot.tooltip = tip ?? "";
		IsModified = cls != null;
	}

	object SafeBoxed() {
		try { return currentProp.boxedValue; }
		catch { return baseline; }
	}

	// ------------------------------------------------------------------ scoping

	void OpenScopeMenu() {
		var menu = new GenericMenu();
		int n = sharedProp.serializedObject.targetObjects.Length;

		menu.AddItem(new UnityEngine.GUIContent($"All {ctx.bucketLabel} instances ({n})"),
			scopeTarget == null, () => SetScope(null));

		var selComp = FindSelectionComponent();
		if (selComp != null)
			menu.AddItem(new UnityEngine.GUIContent("Only: " + selComp.name),
				scopeTarget == selComp, () => SetScope(selComp));
		else
			menu.AddDisabledItem(new UnityEngine.GUIContent("Only: (select an object with this component)"));

		if (ctx.sceneInstances.Length > 0) {
			menu.AddSeparator("");
			menu.AddItem(new UnityEngine.GUIContent("Apply scene override(s) to prefab"), false, () => ForEachOverride(apply: true));
			menu.AddItem(new UnityEngine.GUIContent("Revert scene override(s)"), false, () => ForEachOverride(apply: false));
		}
		menu.ShowAsContext();
	}

	Object FindSelectionComponent() {
		var go = Selection.activeGameObject;
		if (go == null) return null;
		var type = sharedProp.serializedObject.targetObject.GetType();
		var comp = go.GetComponentInChildren(type, true);
		if (comp == null) comp = go.GetComponentInParent(type, true);
		return comp != null && comp.GetType() == type ? comp : null;
	}

	void SetScope(Object single) {
		scopeTarget = single;
		SerializedProperty prop = sharedProp;
		if (single != null) {
			var so = new SerializedObject(single);
			ctx.registerSO?.Invoke(so);
			prop = so.FindProperty(path);
			if (prop == null) { scopeTarget = null; prop = sharedProp; }
		}
		currentProp = prop;

		// swap the control for one bound to the new scope
		int idx = IndexOf(control);
		control.Unbind();
		control.RemoveFromHierarchy();
		control = TuningControlFactory.Create(Spec, currentProp);
		control.TrackPropertyValue(currentProp, OnPropChanged);
		Insert(idx, control);

		UpdateScopePill();
		UpdateDot();
	}

	void UpdateScopePill() {
		if (scopePill == null) return;
		if (scopeTarget != null) {
			scopePill.text = scopeTarget.name;
			scopePill.AddToClassList("is-scoped");
		}
		else {
			scopePill.text = $"{ctx.bucketLabel} ×{sharedProp.serializedObject.targetObjects.Length}";
			scopePill.RemoveFromClassList("is-scoped");
		}
		scopePill.tooltip = "Choose which instances this row edits; apply/revert prefab overrides.";
	}

	void ForEachOverride(bool apply) {
		foreach (var inst in ctx.sceneInstances) {
			if (inst == null) continue;
			using var so = new SerializedObject(inst);
			var p = so.FindProperty(path);
			if (p == null || !p.prefabOverride) continue;
			if (apply) {
				string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(inst);
				if (!string.IsNullOrEmpty(assetPath))
					PrefabUtility.ApplyPropertyOverride(p, assetPath, InteractionMode.UserAction);
			}
			else {
				PrefabUtility.RevertPropertyOverride(p, InteractionMode.UserAction);
			}
		}
		UpdateDot();
	}
}
