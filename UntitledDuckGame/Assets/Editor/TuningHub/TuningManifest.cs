// TuningManifest.cs — THE file to edit when adding/organizing a tuning knob.
//
// Maps component type -> category -> group -> field, with friendly labels, slider
// ranges, units, and apply-mode badges. Fields that exist on a component but are
// not listed here automatically show up in an "Other" card (see TuningDiscovery),
// so forgetting to file a new field never hides it.
//
// NOTE: do NOT add an .asmdef to this folder. This code must stay in the predefined
// Assembly-CSharp-Editor assembly so typeof(...) references to gameplay classes in
// Assembly-CSharp keep compiling (asmdef assemblies cannot reference predefined ones).
using System;
using System.Collections.Generic;
using System.Linq;

public static class TuningManifest {
	const TuningApplyMode Reapply = TuningApplyMode.Reapply;
	const TuningApplyMode NextEvent = TuningApplyMode.NextEvent;
	const TuningApplyMode RestartPlay = TuningApplyMode.RestartPlay;
	const TuningApplyMode EditorOnly = TuningApplyMode.EditorOnly;

	public static readonly IReadOnlyList<TuningCategory> Categories = Build();

	// ---------------------------------------------------------------- the table

	static IReadOnlyList<TuningCategory> Build() => new List<TuningCategory> {

		Cat("Player", "d_Animator Icon",
			G<PlayerDuckController>("Movement",
				F("moveSpeed", "Move Speed", 0f, 15f, unit: "m/s"),
				F("diagonalFactor", "Diagonal Factor", 0.5f, 2f),
				F("horizontalSpeedFactor", "Horizontal Speed Factor", 0f, 3f),
				H("isBrokenFree"),
				D("playerBlockingLayerMask", "declared but never read"),
				D("verticalMinClamp", "clamps never read; movement is collider-driven"),
				D("verticalMaxClamp", "clamps never read"),
				D("horizontalMinClamp", "clamps never read"),
				D("horizontalMaxClamp", "clamps never read")
			),
			G<PlayerDuckController>("Body Physics",
				F("robotMass", "Robot Mass", 1f, 500f, unit: "kg", tip: "Used for arm/body/held-item impulse math"),
				F("bodyImpulseDampFactor", "Body Impulse Damp"),
				F("armImpulseDampFactor", "Arm Impulse Damp"),
				F("pushThreshold", "Push Threshold", 0f, 5f, unit: "m/s")
			),
			G<PlayerDuckController>("Quack",
				F("quackRotation", "Quack Rotation", 0f, 90f, unit: "°", apply: NextEvent, note: "next quack"),
				F("quackDuration", "Quack Duration", 0f, 1f, unit: "s", apply: NextEvent, note: "next quack")
			),
			G<BallSpinner>("Ball",
				F("spinSpeed", "Spin Speed", 0f, 2000f, unit: "°/s")
			)
		),

		Cat("Arm / IK", "d_AvatarPivot",
			G<IsometricRaycaster>("Arm Rotation",
				F("innerZoneRangeX", "Inner Zone X", 0f, 600f, unit: "px"),
				F("minPivotAngle", "Min Pivot Angle", -180f, 0f, unit: "°"),
				F("maxPivotAngle", "Max Pivot Angle", 0f, 180f, unit: "°"),
				F("rotationSmoothSpeed", "Rotation Smoothing", 0f, 30f),
				F("bodyTurnSpeed", "Body Turn Speed", 0f, 720f, unit: "°/s")
			),
			G<IsometricRaycaster>("Arm IK",
				F("minIKX", "Min Reach X"),
				F("maxIKX", "Max Reach X"),
				F("scrollIncrement", "Scroll Increment", 0f, 1f),
				F("innerZoneRangeY", "Inner Zone Y", 0f, 600f, unit: "px"),
				F("minIKY", "Min IK Height", 0f, 3f),
				F("maxIKY", "Max IK Height", 0f, 3f),
				F("ikVerticalSmoothSpeed", "Vertical Smoothing", 0f, 30f)
			),
			G<IsometricRaycaster>("Arm Sweeps",
				F("upperArmBoxWidth", "Upper Box Width", 0f, 0.5f),
				F("upperArmBoxHeight", "Upper Box Height", 0f, 0.5f),
				F("upperArmBoxRotationOffset", "Upper Box Rot Offset"),
				F("upperArmBoxCenterOffset", "Upper Box Center Offset"),
				F("foreArmBoxWidth", "Fore Box Width", 0f, 0.5f),
				F("foreArmBoxHeight", "Fore Box Height", 0f, 0.5f),
				F("foreArmBoxRotationOffset", "Fore Box Rot Offset"),
				F("foreArmBoxCenterOffset", "Fore Box Center Offset"),
				F("sweepIterations", "Sweep Iterations", 1f, 32f),
				F("drawSweepDebug", "Draw Sweep Debug")
			),
			G<IsometricRaycaster>("Arm Collision Masks",
				F("rotationBlockingLayerMask", "Rotation Blockers"),
				F("horizontalIKBlockingLayerMask", "Horizontal IK Blockers"),
				F("verticalIKBlockingLayerMask", "Vertical IK Blockers"),
				F("debugLockKey", "Debug Lock Key", tip: "Also used by BTInputManager — same default key"),
				D("boundaryColor", "declared but never read"),
				D("fadeDistance", "declared but never read")
			)
		),

		Cat("Carry / Fling", "d_Grid.PickingTool",
			G<HeldItemController>("Carry",
				F("transitDuration", "Transit Duration", 0f, 1f, unit: "s", apply: NextEvent, note: "next grab"),
				F("billLength", "Bill Length", 0f, 1f, apply: NextEvent, note: "next grab"),
				F("minGapeDegrees", "Min Gape", 0f, 90f, unit: "°", apply: NextEvent, note: "next grab"),
				F("maxGapeDegrees", "Max Gape", 0f, 90f, unit: "°", apply: NextEvent, note: "next grab"),
				F("mouthAnimDuration", "Mouth Anim Duration", 0f, 1f, unit: "s", apply: NextEvent, note: "next grab"),
				F("armIgnoreSeconds", "Release Grace", 0f, 2f, unit: "s", apply: NextEvent, note: "next release"),
				F("heldItemLayer", "Held Item Layer", apply: NextEvent, note: "next grab"),
				F("maxCarryMass", "Max Carry Mass", 1f, 1000f, unit: "kg", apply: NextEvent, note: "next grab")
			),
			G<HeldItemController>("Fling",
				F("flingSampleWindow", "Sample Window", 0.02f, 0.5f, unit: "s", apply: NextEvent, note: "next release"),
				F("flingPowerScale", "Power Scale", 0f, 5f, apply: NextEvent, note: "next release"),
				F("minFlingSpeed", "Min Fling Speed", 0f, 10f, unit: "m/s", apply: NextEvent, note: "next release"),
				F("maxFlingSpeed", "Max Fling Speed", 0f, 40f, unit: "m/s", apply: NextEvent, note: "next release"),
				F("flingKeepAtMaxMass", "Keep At Max Mass", 0.01f, 0.99f, apply: NextEvent, note: "next release"),
				F("flingTumbleFactor", "Tumble Factor", 0f, 2f, apply: NextEvent, note: "next release")
			),
			G<HeldItemController>("Encumbrance",
				F("enableEncumbrance", "Enabled", apply: NextEvent, note: "next grab"),
				F("encumberLightMass", "Light Mass Floor", 0f, 50f, unit: "kg", apply: NextEvent, note: "next grab"),
				F("minCarryMoveMobility", "Min Move Mobility", 0.05f, 1f, apply: NextEvent, note: "next grab"),
				F("minCarryArmMobility", "Min Arm Mobility", 0.05f, 1f, apply: NextEvent, note: "next grab")
			)
		),

		Cat("Workers", "d_NavMeshAgent Icon",
			G<WorkerAIController>("Patrol",
				F("MoveSpeed", "Move Speed", 0f, 10f, unit: "m/s", apply: Reapply,
					tip: "Cached into the BT blackboard at Awake — the hub re-applies it live via SetStateAtValue"),
				F("ArriveThreshold", "Arrive Threshold", 0.01f, 1f, apply: Reapply),
				F("IsAllowedToMove", "Allowed To Move", apply: Reapply),
				H("IsCollided"),
				H("IsRagdollActive"),
				D("ragdollRBEnabled", "declared but never read")
			),
			G<WorkerAIController>("Detection & Collision",
				F("PlayerDetectionLayerMask", "Detection Mask"),
				F("workerCollisionLayerMask", "Collision Mask")
			),
			G<WorkerAIController>("Recovery & Carry",
				F("RecoverySuppressRange", "Suppress Range", 0f, 10f, unit: "m"),
				F("RecoverySettleSpeed", "Settle Speed", 0f, 3f, unit: "m/s"),
				F("CarryGripSize", "Grip Size", 0f, 2f, apply: NextEvent, note: "next grab"),
				F("CarryGripOffset", "Grip Offset", apply: NextEvent, note: "next grab"),
				F("CarryGripRotation", "Grip Rotation", apply: NextEvent, note: "next grab"),
				F("CarryRotationSpring", "Bite Spring", 0f, 2000f, apply: NextEvent, note: "next grab"),
				F("CarryRotationDamper", "Bite Damper", 0f, 200f, apply: NextEvent, note: "next grab")
			),
			G<WorkerVisualController>("Vision Circle",
				F("paramLerpSpeed", "Param Lerp Speed", 0f, 10f),
				F("innerOpacity", "Inner Opacity", 0f, 1f, apply: NextEvent, note: "next state change"),
				F("outerOpacity", "Outer Opacity", 0f, 1f, apply: NextEvent, note: "next state change"),
				F("colorLerpSpeed", "Color Lerp Speed", 0f, 10f)
			)
		),

		Cat("Alarm", "d_console.warnicon.sml",
			G<GlobalAlarm>("Global Alarm",
				F("cooldownDuration", "Increase Cooldown", 0f, 60f, unit: "s"),
				D("decayRate", "alarm decay is unimplemented — never read")
			),
			Arr<AlarmConfig>("Alarm Levels", "alarmLevels", i => "L" + i,
				"AlarmLevel has 8 values (L0–L7) but this array holds fewer — the top level clamps to the last row.",
				F("playerDetectionAngle", "Detection Angle", 0f, 180f, unit: "°"),
				F("playerDetectionDistance", "Detection Distance", 0f, 20f, unit: "m"),
				F("chaseTimer", "Chase Timer", 0f, 30f, unit: "s"),
				F("playerChaseSpeed", "Chase Speed", 0f, 10f, unit: "m/s")
			)
		),

		Cat("Conveyors", "d_PreMatCylinder",
			G<ConveyorPath>("Belt",
				F("speed", "Belt Speed", 0f, 10f, unit: "u/s"),
				F("loop", "Loop", tip: "Closed loop: items circulate. Off: items fling off the end"),
				F("exitForce", "Exit Force", 0f, 20f),
				F("maxItemMass", "Max Item Mass", 0f, 500f, unit: "kg", tip: "0 = no limit"),
				F("beltWidth", "Belt Width", 0f, 3f, unit: "m"),
				F("lineSubdiv", "Line Subdiv", 1f, 32f, apply: EditorOnly),
				F("cornerSubdiv", "Corner Subdiv", 1f, 32f, apply: EditorOnly),
				H("autoCollectNodes")
			),
			G<ConveyorObjectMover>("Ride",
				F("tangentHalfWindow", "Tangent Half Window", 0f, 1f),
				F("maxTurnRateDegPerSec", "Max Turn Rate", 0f, 1080f, unit: "°/s"),
				F("tiltSettleDegPerSec", "Tilt Settle", 0f, 720f, unit: "°/s"),
				F("rideHeightSettleSpeed", "Height Settle", 0f, 20f, unit: "u/s")
			),
			G<ConveyorObjectMover>("Re-Snap",
				F("snapDistance", "Snap Distance", 0f, 1f, unit: "m"),
				F("restSpeed", "Rest Speed", 0f, 2f, unit: "m/s"),
				F("restAngularSpeed", "Rest Angular Speed", 0f, 5f),
				F("resnapCooldown", "Resnap Cooldown", 0f, 3f, unit: "s"),
				F("resnapCheckInterval", "Check Interval", 0.02f, 1f, unit: "s"),
				F("snapHeightTolerance", "Height Tolerance", 0f, 2f, unit: "m")
			),
			G<ConveyorSegmentVisual>("Belt Visual",
				F("uvPerMeter", "UV Per Meter", 0f, 5f),
				F("localTravelAxis", "Local Travel Axis", apply: RestartPlay),
				F("scrollDirection", "Scroll Direction", apply: RestartPlay)
			)
		),

		Cat("Items & Levers", "d_PrefabModel Icon",
			G<PickupInteractable>("Pickup Grip",
				F("gripSize", "Grip Size", 0f, 2f, apply: NextEvent, note: "next grab", tip: "0 = measured from collider bounds"),
				F("gripOffset", "Grip Offset"),
				F("gripRotation", "Grip Rotation"),
				H("pickupActive")
			),
			G<PickupInteractable>("Impact Reporting",
				F("minImpactSpeed", "Min Impact Speed", 0f, 5f, unit: "m/s"),
				F("minImpactInterval", "Min Impact Interval", 0f, 1f, unit: "s")
			),
			G<LeverInteractable>("Lever Sweep",
				F("minAngle", "Min Angle", -180f, 180f, unit: "°"),
				F("maxAngle", "Max Angle", -180f, 180f, unit: "°"),
				F("localAxis", "Local Axis"),
				F("invertDrag", "Invert Drag"),
				F("travelScreenFraction", "Travel Screen Fraction", 0.05f, 1f)
			),
			G<LeverInteractable>("Lever Feel",
				F("maxLeverSpeed", "Max Lever Speed", 0f, 720f, unit: "°/s"),
				F("tightness", "Tightness", 0.02f, 1f),
				F("tightnessCurve", "Tightness Curve"),
				F("returnToRestOnRelease", "Spring Back"),
				F("returnSpeed", "Spring Back Speed", 0f, 720f, unit: "°/s")
			),
			G<LeverInteractable>("Lever Snap-In",
				F("distanceFromLever", "Stand Distance", 0.2f, 3f, unit: "m", apply: NextEvent, note: "next grab"),
				F("rollSpeed", "Roll Speed", 0f, 20f, unit: "m/s", apply: NextEvent, note: "next grab"),
				F("maxRollDuration", "Max Roll Duration", 0f, 2f, unit: "s", apply: NextEvent, note: "next grab"),
				F("snapTolerance", "Snap Tolerance", 0f, 1f, unit: "m", apply: NextEvent, note: "next grab"),
				F("handReachTime", "Hand Reach Time", 0f, 1f, unit: "s", apply: NextEvent, note: "next grab")
			),
			G<FanRotator>("Fan",
				F("fanSpeed", "Fan Speed", 0f, 360f, unit: "°/s")
			)
		),

		Cat("UI & Camera", "d_Camera Icon",
			G<CursorController>("Cursor",
				F("idleColor", "Idle Color"),
				F("hoverColor", "Hover Color"),
				F("holdColor", "Hold Color"),
				F("defaultScale", "Default Scale"),
				F("hoverScale", "Hover Scale"),
				F("scaleSmoothTime", "Scale Smooth Time", 0f, 0.5f, unit: "s"),
				F("moveSmoothTime", "Move Smooth Time", 0f, 0.5f, unit: "s"),
				F("hoverEngageDistance", "Engage Distance", 0f, 10f, unit: "m"),
				F("stickyCarryPickups", "Sticky Carry")
			),
			G<DebugCameraMover>("Debug Camera",
				F("moveSpeed", "Move Speed", 0f, 50f),
				F("horizontalSpeedFactor", "Horizontal Factor", 0f, 3f)
			)
		),
	};

	// ------------------------------------------------------------ lookup helpers

	static HashSet<Type> componentTypes;
	static Dictionary<Type, HashSet<string>> coveredRoots;

	// All component/SO types the manifest knows about.
	public static HashSet<Type> ComponentTypes {
		get {
			if (componentTypes == null)
				componentTypes = new HashSet<Type>(Categories.SelectMany(c => c.groups).Select(g => g.componentType));
			return componentTypes;
		}
	}

	// Root property names covered by the manifest for a type (incl. dead/hidden),
	// used by discovery to compute the "Other" bucket and by the capture filter.
	public static HashSet<string> CoveredRoots(Type t) {
		if (coveredRoots == null) {
			coveredRoots = new Dictionary<Type, HashSet<string>>();
			foreach (var g in Categories.SelectMany(c => c.groups)) {
				if (!coveredRoots.TryGetValue(g.componentType, out var set))
					coveredRoots[g.componentType] = set = new HashSet<string>();
				if (g.arrayProperty != null) set.Add(g.arrayProperty);
				foreach (var f in g.fields) set.Add(f.fieldName);
			}
		}
		return coveredRoots.TryGetValue(t, out var roots) ? roots : new HashSet<string>();
	}

	// Does any manifest type cover this object (exact or base type)? Used by the
	// play-mode capture filter so subclass instances (e.g. WorkerPickupInteractable)
	// still record under their base type's tunables.
	public static bool CoversInstanceType(Type t) {
		foreach (var known in ComponentTypes)
			if (known.IsAssignableFrom(t)) return true;
		return false;
	}

	public static HashSet<string> CoveredRootsForInstance(Type t) {
		var set = new HashSet<string>();
		foreach (var known in ComponentTypes)
			if (known.IsAssignableFrom(t)) set.UnionWith(CoveredRoots(known));
		return set;
	}

	// ------------------------------------------------------------- table builders

	static TuningCategory Cat(string name, string icon, params TuningGroup[] groups) =>
		new TuningCategory { name = name, icon = icon, groups = groups.ToList() };

	static TuningGroup G<T>(string title, params TuningFieldSpec[] fields) where T : UnityEngine.Object =>
		new TuningGroup { componentType = typeof(T), title = title, fields = fields.ToList() };

	static TuningGroup Arr<T>(string title, string arrayProperty, Func<int, string> elementLabel,
			string note, params TuningFieldSpec[] fields) where T : UnityEngine.Object =>
		new TuningGroup {
			componentType = typeof(T), title = title, fields = fields.ToList(),
			arrayProperty = arrayProperty, elementLabel = elementLabel, note = note,
		};

	static TuningFieldSpec F(string name, string label, float min = float.NaN, float max = float.NaN,
			string unit = null, TuningApplyMode apply = TuningApplyMode.Live, string note = null, string tip = null) =>
		new TuningFieldSpec {
			fieldName = name, label = label,
			hasRange = !float.IsNaN(min) && !float.IsNaN(max), min = min, max = max,
			unit = unit, apply = apply, applyNote = note, tooltip = tip,
		};

	static TuningFieldSpec D(string name, string reason) =>
		new TuningFieldSpec {
			fieldName = name, label = name, dead = true, deadReason = reason,
			apply = TuningApplyMode.EditorOnly,
		};

	static TuningFieldSpec H(string name) =>
		new TuningFieldSpec { fieldName = name, label = name, hidden = true };
}
