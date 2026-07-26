// TuningValueCodec.cs — writes a PropertyModification-format value string back
// onto a SerializedProperty. Snapshot entries store LEAF paths (e.g. "gripOffset.x"
// arrives as a Float), so only scalar leaf types need handling here.
using System.Globalization;
using UnityEditor;

public static class TuningValueCodec {
	public static bool TrySet(SerializedProperty p, string value) {
		if (p == null || value == null) return false;

		switch (p.propertyType) {
			case SerializedPropertyType.Float:
				if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return false;
				p.floatValue = f;
				return true;

			case SerializedPropertyType.Integer:
			case SerializedPropertyType.LayerMask:
			case SerializedPropertyType.ArraySize:
			case SerializedPropertyType.Character:
				if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return false;
				p.intValue = i;
				return true;

			case SerializedPropertyType.Enum:
				if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int e)) return false;
				p.intValue = e;
				return true;

			case SerializedPropertyType.Boolean:
				if (int.TryParse(value, out int b)) { p.boolValue = b != 0; return true; }
				if (bool.TryParse(value, out bool bv)) { p.boolValue = bv; return true; }
				return false;

			case SerializedPropertyType.String:
				p.stringValue = value;
				return true;

			default:
				return false;
		}
	}

	public static string Describe(SerializedProperty p) {
		if (p == null) return "?";
		return p.propertyType switch {
			SerializedPropertyType.Float => p.floatValue.ToString("0.###", CultureInfo.InvariantCulture),
			SerializedPropertyType.Boolean => p.boolValue ? "on" : "off",
			SerializedPropertyType.Integer => p.intValue.ToString(CultureInfo.InvariantCulture),
			_ => p.displayName,
		};
	}
}
