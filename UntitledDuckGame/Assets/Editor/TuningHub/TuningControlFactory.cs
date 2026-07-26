// TuningControlFactory.cs — maps (SerializedPropertyType, TuningFieldSpec) to a
// bound UI Toolkit control. One place to change control policy.
//
// Ranged numerics use Slider/SliderInt with showInputField — a single real
// BaseField<T>, so mixed-value (em-dash) display and prefab-override styling come
// free from the binding system; a hand-rolled slider+field composite gets neither.
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public static class TuningControlFactory {
	public static VisualElement Create(TuningFieldSpec spec, SerializedProperty prop) {
		VisualElement control;

		switch (prop.propertyType) {
			case SerializedPropertyType.Float when spec.hasRange: {
				var (lo, hi) = ExpandRange(spec, prop, p => p.floatValue);
				var s = new Slider(lo, hi) { showInputField = true };
				s.BindProperty(prop);
				control = s;
				break;
			}
			case SerializedPropertyType.Float: {
				var f = new FloatField();
				f.BindProperty(prop);
				control = f;
				break;
			}
			case SerializedPropertyType.Integer when spec.hasRange: {
				var (lo, hi) = ExpandRange(spec, prop, p => p.intValue);
				var s = new SliderInt((int)lo, (int)hi) { showInputField = true };
				s.BindProperty(prop);
				control = s;
				break;
			}
			case SerializedPropertyType.Integer: {
				var f = new IntegerField();
				f.BindProperty(prop);
				control = f;
				break;
			}
			case SerializedPropertyType.Boolean: {
				var t = new Toggle();
				t.BindProperty(prop);
				control = t;
				break;
			}
			default: {
				// enum, color, vector, curve, gradient, layer mask, …
				// PropertyField also gives the built-in right-click prefab context menu.
				var pf = new PropertyField(prop, string.Empty);
				pf.BindProperty(prop);
				control = pf;
				break;
			}
		}

		control.AddToClassList("th-control");
		return control;
	}

	// If a target's current value sits outside the authored range, widen the slider
	// to include it — otherwise binding would clamp and silently WRITE the clamped
	// value the moment the row is built.
	static (float lo, float hi) ExpandRange(TuningFieldSpec spec, SerializedProperty prop,
			System.Func<SerializedProperty, float> read) {
		float lo = spec.min, hi = spec.max;
		foreach (var target in prop.serializedObject.targetObjects) {
			if (target == null) continue;
			using var single = new SerializedObject(target);
			var p = single.FindProperty(prop.propertyPath);
			if (p == null) continue;
			float v = read(p);
			if (v < lo) lo = v;
			if (v > hi) hi = v;
		}
		return (lo, hi);
	}
}
