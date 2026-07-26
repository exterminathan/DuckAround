using UnityEditor;
using UnityEngine;

// Inspector button for ConveyorPath: reverse the path's travel direction in one
// click — flips child/list order, 180°s every node, mirrors corner turns.
[CustomEditor(typeof(ConveyorPath))]
[CanEditMultipleObjects]
public class ConveyorPathEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Reverse Path Direction")) {
            foreach (Object t in targets)
                ((ConveyorPath)t).ReversePathDirection();
        }
    }
}
