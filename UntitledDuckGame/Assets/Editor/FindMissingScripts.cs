using UnityEditor;
using UnityEngine;
using System.IO;

public class FindMissingScriptsInAssets {
    [MenuItem("Tools/Find Missing Scripts in Assets")]
    static void FindMissingScripts() {
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGUIDs) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            foreach (Component c in components) {
                if (c == null) {
                    Debug.LogWarning($"Missing script in prefab: {path}", prefab);
                }
            }
        }
    }
}
