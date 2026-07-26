// TuningHubWindow.cs — Tools ▸ Tuning Hub. One window that surfaces every tunable
// knob in the project (per TuningManifest), edits the real serialized fields on
// scene objects / prefab assets / ScriptableObjects, and — with
// PlayModeCaptureService — keeps play-mode edits instead of losing them.
//
// Domain-reload rules (see HotReloadRemote.cs for why this matters here):
//  - every SerializedObject/row cache is [NonSerialized]; CreateGUI() is the single
//    build entry point and reruns after every reload
//  - UI state that must survive a reload lives in SessionState (TuningHubState)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class TuningHubWindow : EditorWindow {
	const string UssPath = "Assets/Editor/TuningHub/TuningHubWindow.uss";
	const int MaxTargets = 64;

	[NonSerialized] readonly List<SerializedObject> liveSOs = new();
	[NonSerialized] readonly List<TuningCardView> cards = new();
	[NonSerialized] bool rebuildScheduled;

	ScrollView content;
	VisualElement navList;
	ToolbarSearchField searchField;
	Label banner;
	VisualElement contextBar;
	Button sharedBtn, selectionBtn, snapshotPill;

	[MenuItem("Tools/Tuning Hub/Hub", priority = 0)]
	static void Open() {
		var w = GetWindow<TuningHubWindow>("Tuning Hub");
		w.minSize = new Vector2(560, 320);
	}

	// ------------------------------------------------------------------ lifecycle

	void OnEnable() {
		EditorApplication.playModeStateChanged += OnPlayModeChanged;
		Selection.selectionChanged += OnSelectionChanged;
		EditorApplication.hierarchyChanged += OnHierarchyChanged;
		AssemblyReloadEvents.beforeAssemblyReload += FlushAll;
		PlayModeCaptureService.Changed += RefreshDots;
		Undo.undoRedoPerformed += RefreshDots;
	}

	void OnDisable() {
		EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		Selection.selectionChanged -= OnSelectionChanged;
		EditorApplication.hierarchyChanged -= OnHierarchyChanged;
		AssemblyReloadEvents.beforeAssemblyReload -= FlushAll;
		PlayModeCaptureService.Changed -= RefreshDots;
		Undo.undoRedoPerformed -= RefreshDots;
		FlushAll();
		DisposeSOs();
	}

	void CreateGUI() {
		var root = rootVisualElement;
		root.Clear();

		var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
		if (uss == null) {
			string guid = AssetDatabase.FindAssets("TuningHubWindow t:StyleSheet").FirstOrDefault();
			if (guid != null) uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
		}
		if (uss != null) root.styleSheets.Add(uss);
		else Debug.LogError("[TuningHub] TuningHubWindow.uss not found — window will render unstyled.");

		root.AddToClassList("th-root");
		root.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
		TuningHubState.ApplyUiScaleClass(root);

		BuildChrome(root);
		TuningManifestValidator.RunOnce();
		RebuildContent();

		root.schedule.Execute(FlushAssetDirty).Every(5000);
	}

	// --------------------------------------------------------------------- chrome

	void BuildChrome(VisualElement root) {
		var shell = new VisualElement();
		shell.AddToClassList("th-shell");
		root.Add(shell);

		// ---- sidebar
		var sidebar = new VisualElement();
		sidebar.AddToClassList("th-sidebar");
		shell.Add(sidebar);

		var title = new Label("TUNING HUB");
		title.AddToClassList("th-sidebar-title");
		sidebar.Add(title);

		var modeSwitch = new VisualElement();
		modeSwitch.AddToClassList("th-mode-switch");
		sharedBtn = new Button(() => SetMode(false)) { text = "Shared" };
		selectionBtn = new Button(() => SetMode(true)) { text = "Selection" };
		sharedBtn.AddToClassList("th-mode-btn");
		selectionBtn.AddToClassList("th-mode-btn");
		modeSwitch.Add(sharedBtn);
		modeSwitch.Add(selectionBtn);
		sidebar.Add(modeSwitch);

		navList = new ScrollView();
		navList.AddToClassList("th-nav");
		sidebar.Add(navList);
		foreach (var cat in TuningManifest.Categories) {
			var item = new Button(() => SetCategory(cat.name));
			item.AddToClassList("th-nav-item");
			var label = new Label(cat.name);
			label.AddToClassList("th-nav-label");
			item.Add(label);
			int count = cat.groups.Sum(g => g.fields.Count(f => !f.dead && !f.hidden));
			var countEl = new Label(count.ToString());
			countEl.AddToClassList("th-nav-count");
			item.Add(countEl);
			item.userData = cat.name;
			navList.Add(item);
		}

		var footer = new VisualElement();
		footer.AddToClassList("th-sidebar-footer");
		footer.Add(LegendRow("is-snapshot", "play-mode change"));
		footer.Add(LegendRow("is-session", "changed this session"));
		footer.Add(LegendRow("is-override", "prefab override"));
		snapshotPill = new Button(TuningSnapshotReviewWindow.Open);
		snapshotPill.AddToClassList("th-snapshot-pill");
		footer.Add(snapshotPill);
		sidebar.Add(footer);

		// ---- main column
		var main = new VisualElement();
		main.AddToClassList("th-main");
		shell.Add(main);

		var toolbar = new Toolbar();
		toolbar.AddToClassList("th-toolbar");
		searchField = new ToolbarSearchField();
		searchField.AddToClassList("th-search");
		searchField.SetValueWithoutNotify(TuningHubState.SearchText);
		searchField.RegisterValueChangedCallback(e => {
			TuningHubState.SearchText = e.newValue;
			rootVisualElement.schedule.Execute(ApplyFilter).ExecuteLater(120);
		});
		toolbar.Add(searchField);

		var spacer = new VisualElement();
		spacer.style.flexGrow = 1;
		toolbar.Add(spacer);

		var options = new ToolbarMenu { text = "Options" };
		options.menu.AppendAction("Write to Prefab Assets by Default",
			_ => { TuningHubState.WriteToScene = !TuningHubState.WriteToScene; RebuildContent(); },
			_ => TuningHubState.WriteToScene ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Checked);
		options.menu.AppendAction("Show Dead Fields",
			_ => { TuningHubState.ShowDead = !TuningHubState.ShowDead; RebuildContent(); },
			_ => TuningHubState.ShowDead ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
		options.menu.AppendAction("Show Uncategorized",
			_ => { TuningHubState.ShowUncategorized = !TuningHubState.ShowUncategorized; RebuildContent(); },
			_ => TuningHubState.ShowUncategorized ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
		options.menu.AppendAction("Modified Only",
			_ => { TuningHubState.ModifiedOnly = !TuningHubState.ModifiedOnly; ApplyFilter(); },
			_ => TuningHubState.ModifiedOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
		options.menu.AppendSeparator();
		options.menu.AppendAction("UI Scale/Compact", _ => SetUiScale(0), _ => ScaleStatus(0));
		options.menu.AppendAction("UI Scale/Medium", _ => SetUiScale(1), _ => ScaleStatus(1));
		options.menu.AppendAction("UI Scale/Large", _ => SetUiScale(2), _ => ScaleStatus(2));
		options.menu.AppendSeparator();
		options.menu.AppendAction("Validate Manifest", _ => TuningManifestValidator.ValidateNow());
		options.menu.AppendAction("Snapshot Review…", _ => TuningSnapshotReviewWindow.Open());
		toolbar.Add(options);

		var refresh = new ToolbarButton(() => {
			TuningTargetResolver.Invalidate();
			TuningDiscovery.ClearCache();
			RebuildContent();
		}) { text = "Refresh" };
		toolbar.Add(refresh);
		main.Add(toolbar);

		banner = new Label();
		banner.AddToClassList("th-banner");
		main.Add(banner);

		contextBar = new VisualElement();
		contextBar.AddToClassList("th-context");
		main.Add(contextBar);

		content = new ScrollView();
		content.AddToClassList("th-content");
		main.Add(content);
	}

	void SetUiScale(int scale) {
		TuningHubState.UiScale = scale;
		TuningHubState.ApplyUiScaleClass(rootVisualElement); // pure class toggle — no rebuild needed
	}

	DropdownMenuAction.Status ScaleStatus(int scale) =>
		TuningHubState.UiScale == scale ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;

	VisualElement LegendRow(string dotClass, string text) {
		var row = new VisualElement();
		row.AddToClassList("th-legend-row");
		var dot = new VisualElement();
		dot.AddToClassList("th-dot");
		dot.AddToClassList(dotClass);
		row.Add(dot);
		var label = new Label(text);
		label.AddToClassList("th-legend-label");
		row.Add(label);
		return row;
	}

	// ------------------------------------------------------------------- rebuild

	void SetMode(bool selection) {
		TuningHubState.SelectionMode = selection;
		RebuildContent();
	}

	void SetCategory(string name) {
		TuningHubState.ActiveCategory = name;
		if (TuningHubState.SelectionMode) TuningHubState.SelectionMode = false;
		RebuildContent();
	}

	void ScheduleRebuild() {
		if (rebuildScheduled || rootVisualElement == null) return;
		rebuildScheduled = true;
		rootVisualElement.schedule.Execute(() => {
			rebuildScheduled = false;
			RebuildContent();
		}).ExecuteLater(250);
	}

	void RebuildContent() {
		if (content == null) return;
		content.Clear();
		cards.Clear();
		DisposeSOs();

		UpdateBanner();
		UpdateModeUI();
		UpdateSnapshotPill();

		try {
			if (TuningHubState.SelectionMode) BuildSelectionView();
			else BuildCategoryView(ActiveCategory());
		}
		catch (Exception e) {
			Debug.LogWarning("[TuningHub] Rebuild failed (retrying next frame): " + e.Message);
			rootVisualElement.schedule.Execute(RebuildContent).ExecuteLater(100);
			return;
		}

		ApplyFilter();
	}

	TuningCategory ActiveCategory() {
		string name = TuningHubState.ActiveCategory;
		return TuningManifest.Categories.FirstOrDefault(c => c.name == name) ?? TuningManifest.Categories[0];
	}

	void UpdateModeUI() {
		bool sel = TuningHubState.SelectionMode;
		sharedBtn.EnableInClassList("is-selected", !sel);
		selectionBtn.EnableInClassList("is-selected", sel);

		string active = ActiveCategory().name;
		foreach (var child in navList.Children())
			child.EnableInClassList("is-selected", !sel && (string)child.userData == active);

		contextBar.Clear();
		contextBar.style.display = DisplayStyle.None;
	}

	void UpdateBanner() {
		var stage = PrefabStageUtility.GetCurrentPrefabStage();
		if (EditorApplication.isPlayingOrWillChangePlaymode) {
			banner.text = "▶ Play mode — editing live scene instances. Changes are captured and offered for keeping on exit.";
			banner.style.display = DisplayStyle.Flex;
		}
		else if (stage != null) {
			banner.text = "Prefab stage open: " + stage.prefabContentsRoot.name;
			banner.style.display = DisplayStyle.Flex;
		}
		else {
			banner.style.display = DisplayStyle.None;
		}
	}

	void UpdateSnapshotPill() {
		int n = PlayModeCaptureService.PendingCount;
		snapshotPill.text = n > 0 ? $"● {n} pending play change(s)" : "snapshot: idle";
		snapshotPill.EnableInClassList("is-active", n > 0);
	}

	// -------------------------------------------------------------- shared view

	void BuildCategoryView(TuningCategory cat) {
		var seenTypes = new HashSet<Type>();
		foreach (var group in cat.groups) {
			BuildGroupCard(group, null);
			seenTypes.Add(group.componentType);
		}

		if (TuningHubState.ShowUncategorized)
			foreach (var type in seenTypes)
				BuildUncategorizedCard(type);
	}

	void BuildGroupCard(TuningGroup group, Object[] explicitTargets) {
		var res = TuningTargetResolver.Resolve(group.componentType);

		Object[] targets;
		string bucketLabel;
		bool bucketIsAssets;

		if (explicitTargets != null) {
			targets = explicitTargets;
			bucketLabel = "Selected";
			bucketIsAssets = false;
		}
		else if (res.isScriptableObject) {
			targets = res.assetComponents;
			bucketLabel = "Asset";
			bucketIsAssets = true;
		}
		else if (EditorApplication.isPlayingOrWillChangePlaymode) {
			// prefab-asset writes don't reach already-instantiated objects — force scene
			targets = res.sceneComponents.Length > 0 ? res.sceneComponents : Array.Empty<Object>();
			bucketLabel = "Scene";
			bucketIsAssets = false;
		}
		else if (!TuningHubState.WriteToScene && res.assetComponents.Length > 0) {
			targets = res.assetComponents;
			bucketLabel = "Prefab";
			bucketIsAssets = true;
		}
		else if (res.sceneComponents.Length > 0) {
			targets = res.sceneComponents;
			bucketLabel = "Scene";
			bucketIsAssets = false;
		}
		else {
			targets = res.assetComponents;
			bucketLabel = "Prefab";
			bucketIsAssets = true;
		}

		if (targets.Length == 0) {
			var empty = new TuningCardView(group.title, group.componentType.Name);
			empty.AddNote(EditorApplication.isPlaying
				? "No live instances in the scene."
				: "No instances found in loaded scenes or prefabs.");
			content.Add(empty);
			cards.Add(empty);
			return;
		}
		if (targets.Length > MaxTargets) targets = targets.Take(MaxTargets).ToArray();

		var so = new SerializedObject(targets);
		liveSOs.Add(so);

		var ctx = new TuningRowContext {
			sceneInstances = bucketIsAssets ? res.sceneComponents : Array.Empty<Object>(),
			bucketIsAssets = bucketIsAssets,
			bucketLabel = bucketLabel,
			allowScopeMenu = explicitTargets == null && !res.isScriptableObject,
			registerSO = liveSOs.Add,
		};

		string overrideWarn = null;
		if (bucketIsAssets && res.sceneComponents.Length > 0) {
			int overrides = CountSceneOverrides(group, res.sceneComponents);
			if (overrides > 0) overrideWarn = $"{overrides} scene override(s)";
		}

		var card = new TuningCardView(group.title, $"{group.componentType.Name} · {bucketLabel} ×{targets.Length}", overrideWarn);
		if (!string.IsNullOrEmpty(group.note)) card.AddNote(group.note);

		if (group.arrayProperty != null) BuildArrayRows(card, group, so, ctx);
		else BuildRows(card, group, so, ctx);

		content.Add(card);
		cards.Add(card);
	}

	void BuildRows(TuningCardView card, TuningGroup group, SerializedObject so, TuningRowContext ctx) {
		foreach (var spec in group.fields) {
			if (spec.hidden) continue;
			if (spec.dead && !TuningHubState.ShowDead) continue;
			var prop = so.FindProperty(spec.fieldName);
			if (prop == null) {
				card.AddWarning($"field '{spec.fieldName}' not found — update TuningManifest");
				continue;
			}
			card.AddRow(new TuningFieldRow(spec, prop, ctx));
		}
	}

	void BuildArrayRows(TuningCardView card, TuningGroup group, SerializedObject so, TuningRowContext ctx) {
		var arr = so.FindProperty(group.arrayProperty);
		if (arr == null || !arr.isArray) {
			card.AddWarning($"array '{group.arrayProperty}' not found — update TuningManifest");
			return;
		}

		// AlarmLevel enum has 8 values; a shorter array silently clamps at the top.
		if (group.componentType == typeof(AlarmConfig) && arr.arraySize < 8)
			card.AddWarning($"only {arr.arraySize} of 8 alarm levels have data — L7 clamps to the last row");

		for (int i = 0; i < arr.arraySize; i++) {
			string label = group.elementLabel != null ? group.elementLabel(i) : "[" + i + "]";
			card.AddSubHeader(label);
			foreach (var spec in group.fields) {
				if (spec.hidden || (spec.dead && !TuningHubState.ShowDead)) continue;
				string path = $"{group.arrayProperty}.Array.data[{i}].{spec.fieldName}";
				var prop = so.FindProperty(path);
				if (prop == null) {
					card.AddWarning($"field '{spec.fieldName}' not found in {group.arrayProperty}[{i}]");
					continue;
				}
				card.AddRow(new TuningFieldRow(spec, prop, ctx, labelOverride: spec.label));
			}
		}
	}

	void BuildUncategorizedCard(Type type) {
		var res = TuningTargetResolver.Resolve(type);
		var rep = res.assetComponents.FirstOrDefault() ?? res.sceneComponents.FirstOrDefault();
		if (rep == null) return;

		var covered = TuningManifest.CoveredRoots(type);
		var uncovered = TuningDiscovery.TunableRoots(rep).Where(r => !covered.Contains(r)).ToList();
		if (uncovered.Count == 0) return;

		var group = new TuningGroup {
			componentType = type,
			title = "Other — " + type.Name,
			fields = uncovered.Select(n => new TuningFieldSpec {
				fieldName = n,
				label = ObjectNames.NicifyVariableName(n),
				tooltip = "Not filed in TuningManifest yet.",
			}).ToList(),
		};
		BuildGroupCard(group, null);
	}

	int CountSceneOverrides(TuningGroup group, Object[] sceneInstances) {
		int count = 0;
		foreach (var inst in sceneInstances) {
			if (inst == null) continue;
			using var so = new SerializedObject(inst);
			foreach (var spec in group.fields) {
				if (spec.hidden || spec.dead) continue;
				string path = group.arrayProperty != null ? group.arrayProperty : spec.fieldName;
				var p = so.FindProperty(path);
				if (p != null && p.prefabOverride) { count++; break; } // count instances, not fields
			}
		}
		return count;
	}

	// ------------------------------------------------------------ selection view

	void BuildSelectionView() {
		var go = Selection.activeGameObject;

		contextBar.Clear();
		if (go == null) {
			contextBar.style.display = DisplayStyle.None;
			var hint = new Label("Select an object in the Hierarchy to see its tunable components.");
			hint.AddToClassList("th-empty");
			content.Add(hint);
			return;
		}

		contextBar.style.display = DisplayStyle.Flex;
		var name = new Label(go.name);
		name.AddToClassList("th-context-name");
		contextBar.Add(name);

		bool foundAny = false;
		foreach (var cat in TuningManifest.Categories) {
			foreach (var group in cat.groups) {
				if (typeof(ScriptableObject).IsAssignableFrom(group.componentType)) continue;
				var comps = go.GetComponentsInChildren(group.componentType, true)
					.Where(c => c.GetType() == group.componentType).Cast<Object>().ToArray();
				if (comps.Length == 0) {
					var parent = go.GetComponentInParent(group.componentType, true);
					if (parent != null && parent.GetType() == group.componentType) comps = new Object[] { parent };
				}
				if (comps.Length == 0) continue;

				foundAny = true;
				var chip = new Label(group.componentType.Name);
				chip.AddToClassList("th-context-chip");
				if (contextBar.Children().OfType<Label>().All(l => l.text != group.componentType.Name || !l.ClassListContains("th-context-chip")))
					contextBar.Add(chip);
				BuildGroupCard(group, comps);
			}
		}

		if (!foundAny) {
			var none = new Label($"'{go.name}' has no tunable components (checked children and parents).");
			none.AddToClassList("th-empty");
			content.Add(none);
		}
	}

	// -------------------------------------------------------------------- filter

	void ApplyFilter() {
		string q = (TuningHubState.SearchText ?? "").Trim().ToLowerInvariant();
		bool modOnly = TuningHubState.ModifiedOnly;

		Func<TuningFieldRow, bool> pred = null;
		if (q.Length > 0 || modOnly)
			pred = row => (q.Length == 0 || row.Haystack.Contains(q)) && (!modOnly || row.IsModified);

		foreach (var card in cards) card.ApplyFilter(pred);
	}

	// ------------------------------------------------------------------ plumbing

	void OnPlayModeChanged(PlayModeStateChange change) {
		if (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode) {
			TuningTargetResolver.Invalidate();
			ScheduleRebuild();
		}
	}

	void OnSelectionChanged() {
		if (TuningHubState.SelectionMode) ScheduleRebuild();
	}

	void OnHierarchyChanged() {
		TuningTargetResolver.Invalidate();
		if (!EditorApplication.isPlayingOrWillChangePlaymode) ScheduleRebuild();
	}

	void RefreshDots() {
		UpdateSnapshotPill();
		foreach (var card in cards)
			foreach (var row in card.Rows)
				row.UpdateDot();
		if (TuningHubState.ModifiedOnly) ApplyFilter();
	}

	void FlushAll() {
		foreach (var so in liveSOs) {
			try {
				if (so != null && so.targetObject != null && so.hasModifiedProperties)
					so.ApplyModifiedProperties();
			}
			catch { /* stale SO — nothing to flush */ }
		}
		FlushAssetDirty();
	}

	void FlushAssetDirty() {
		foreach (var so in liveSOs) {
			try {
				if (so == null || so.targetObject == null) continue;
				foreach (var t in so.targetObjects)
					if (t != null && EditorUtility.IsPersistent(t))
						AssetDatabase.SaveAssetIfDirty(t);
			}
			catch { /* stale SO — skip */ }
		}
	}

	void DisposeSOs() {
		foreach (var so in liveSOs) {
			try { so?.Dispose(); }
			catch { }
		}
		liveSOs.Clear();
	}
}
