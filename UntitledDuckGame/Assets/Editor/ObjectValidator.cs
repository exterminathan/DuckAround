using UnityEditor;
using UnityEngine;
using System.Reflection;

public class ObjectValidatorWindow : EditorWindow {
    private GameObject objectToValidate;

    [MenuItem("Tools/Object Validator")]
    public static void ShowWindow() {
        GetWindow<ObjectValidatorWindow>("Object Validator");
    }

    private void OnGUI() {
        EditorGUILayout.LabelField("Object Validator", EditorStyles.boldLabel);
        objectToValidate = (GameObject)EditorGUILayout.ObjectField("Target GameObject", objectToValidate, typeof(GameObject), true);

        if (objectToValidate == null) return;

        if (GUILayout.Button("Validate")) {
            ValidateObject(objectToValidate);
        }
    }

    private void ValidateObject(GameObject obj) {
        MonoBehaviour[] components = obj.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour mb in components) {
            if (mb == null) continue;

            var fields = mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields) {
                if (field.IsInitOnly || field.IsLiteral) continue;

                bool isSerialized =
                    field.IsPublic ||
                    field.GetCustomAttribute<SerializeField>() != null;

                if (isSerialized) {
                    object value = field.GetValue(mb);
                    if (value == null) {
                        Debug.LogWarning($"[{mb.name}] Missing reference on {mb.GetType().Name}.{field.Name}", mb);
                    }
                }
            }
        }

        Debug.Log("Validation complete.");
    }
}
