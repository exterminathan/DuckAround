using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System.Text;

public class HierarchyPrint : EditorWindow {
    private GameObject parentObject;
    private const string OutputPath = "Assets/HierarchyDump.txt";

    [MenuItem("Tools/Hierarchy Inspector")]
    public static void ShowWindow() {
        GetWindow<HierarchyPrint>("Hierarchy Inspector");
    }

    private void OnGUI() {
        GUILayout.Label("Inspect Hierarchy", EditorStyles.boldLabel);
        parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);

        if (GUILayout.Button("Dump to File")) {
            if (parentObject == null) {
                Debug.LogWarning("No parent object assigned.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Starting Hierarchy Dump for {parentObject.name} ---");
            InspectGameObject(parentObject, 0, sb);
            sb.AppendLine($"--- Finished Hierarchy Dump ---");

            File.WriteAllText(OutputPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"Hierarchy info written to {OutputPath}");
        }
    }

    private void InspectGameObject(GameObject obj, int indentLevel, StringBuilder sb) {
        string indent = new string(' ', indentLevel * 2);
        sb.AppendLine($"{indent}GameObject: {obj.name}");

        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components) {
            if (comp == null) continue;
            sb.AppendLine($"{indent}  Component: {comp.GetType().Name}");

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            FieldInfo[] fields = comp.GetType().GetFields(flags);
            PropertyInfo[] props = comp.GetType().GetProperties(flags);

            foreach (FieldInfo field in fields) {
                object value = field.GetValue(comp);
                sb.AppendLine($"{indent}    Field: {field.Name} = {value}");
            }

            foreach (PropertyInfo prop in props) {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                try {
                    object value = prop.GetValue(comp, null);
                    sb.AppendLine($"{indent}    Property: {prop.Name} = {value}");
                }
                catch { }
            }
        }

        foreach (Transform child in obj.transform) {
            InspectGameObject(child.gameObject, indentLevel + 1, sb);
        }
    }
}
