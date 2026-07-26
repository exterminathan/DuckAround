// TuningSchema.cs — pure data types for the Tuning Hub manifest.
// No editor API calls here; the manifest (TuningManifest.cs) builds these,
// the window (TuningHubWindow.cs) renders them.
using System;
using System.Collections.Generic;

// How a runtime edit to this field actually lands in gameplay. Drives the row badge.
public enum TuningApplyMode {
	Live,        // read every frame — edits apply instantly
	Reapply,     // cached at Awake/Start, but the hub auto-invokes a re-apply hook (TuningReapplyHooks)
	NextEvent,   // read at the moment an action fires (grab, release, capture) — applies to the NEXT one
	RestartPlay, // consumed in Awake/Start with no re-apply path — needs a play restart
	EditorOnly,  // only meaningful at edit time
}

public class TuningFieldSpec {
	public string fieldName;   // serialized property name (relative to the element in an array group)
	public string label;
	public bool hasRange;
	public float min, max;
	public string unit;        // display suffix: "m/s", "°", "s", "kg", "px"
	public TuningApplyMode apply = TuningApplyMode.Live;
	public string applyNote;   // badge text override, e.g. "next grab"
	public string tooltip;
	public bool dead;          // serialized but never read by gameplay — hidden unless Show Dead Fields
	public string deadReason;
	public bool hidden;        // runtime-state flags etc: counted as covered, never rendered
}

public class TuningGroup {
	public Type componentType;
	public string title;
	public List<TuningFieldSpec> fields = new();

	// Array-expanded group (AlarmConfig.alarmLevels): fields are relative to each
	// element; the window expands one sub-block per element at build time.
	public string arrayProperty;
	public Func<int, string> elementLabel;

	public string note;        // info line rendered at the top of the card
}

public class TuningCategory {
	public string name;
	public string icon;        // editor icon name for the sidebar (optional)
	public List<TuningGroup> groups = new();
}
