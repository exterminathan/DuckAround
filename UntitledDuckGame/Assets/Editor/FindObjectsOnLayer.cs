using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FindObjectsOnLayer : EditorWindow {
    private int layerToFind = 0;
    private List<GameObject> foundObjects = new List<GameObject>();

    [MenuItem("Tools/Find Objects On Layer")]
    public static void ShowWindow() {
        GetWindow<FindObjectsOnLayer>("Find Objects On Layer");
    }

    private void OnGUI() {
        GUILayout.Label("Select Layer to Search", EditorStyles.boldLabel);
        layerToFind = EditorGUILayout.LayerField("Layer", layerToFind);

        if (GUILayout.Button("Find GameObjects")) {
            FindObjOnLayer(layerToFind);
        }

        if (foundObjects.Count > 0 && GUILayout.Button("Select All Found Objects")) {
            Selection.objects = foundObjects.ToArray();
        }
    }

    private void FindObjOnLayer(int layer) {
        foundObjects.Clear();
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (GameObject obj in allObjects) {
            if (obj.layer == layer) {
                foundObjects.Add(obj);
                Debug.Log($"Found: {obj.name}", obj);
                count++;
            }
        }

        Debug.Log($"Total objects on layer '{LayerMask.LayerToName(layer)}': {count}");
    }
}
