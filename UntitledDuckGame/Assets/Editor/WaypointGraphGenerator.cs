using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class WaypointGraphGenerator : EditorWindow {
    // UI Parameters
    private GameObject waypointPrefab;
    private Vector2 areaSize = new Vector2(15, 15);
    private int nodeCount = 10;

    // Connectivity Settings
    [Range(0f, 1f)] private float twoWayPercentage = 0.15f;
    [Range(0f, 1f)] private float oneWayPercentage = 0.10f;
    [Range(0f, 20f)] private float maxConnectionDistance = 15f;

    // Assignment Settings
    [Range(0f, 1f)] private float managerWaypointFillPercentage = 0.4f;

    [MenuItem("Tools/Waypoint Graph Generator")]
    public static void ShowWindow() {
        GetWindow<WaypointGraphGenerator>("Graph Gen");
    }

    private void OnGUI() {
        GUILayout.Label("Graph Settings", EditorStyles.boldLabel);

        waypointPrefab = (GameObject)EditorGUILayout.ObjectField("Waypoint Prefab", waypointPrefab, typeof(GameObject), false);
        areaSize = EditorGUILayout.Vector2Field("Area Size (X, Z)", areaSize);
        nodeCount = EditorGUILayout.IntField("Node Count", nodeCount);

        GUILayout.Space(10);
        GUILayout.Label("Connectivity", EditorStyles.boldLabel);
        twoWayPercentage = EditorGUILayout.Slider("Two-Way Density %", twoWayPercentage, 0f, 1f);
        oneWayPercentage = EditorGUILayout.Slider("One-Way Density %", oneWayPercentage, 0f, 1f);
        maxConnectionDistance = EditorGUILayout.FloatField("Max Edge Distance", maxConnectionDistance);

        GUILayout.Space(10);
        GUILayout.Label("Manager Integration", EditorStyles.boldLabel);
        managerWaypointFillPercentage = EditorGUILayout.Slider("Manager Fill %", managerWaypointFillPercentage, 0f, 1f);

        GUILayout.Space(20);

        if (GUILayout.Button("Generate Graph")) {
            if (waypointPrefab == null) {
                Debug.LogError("Please assign a Waypoint Prefab first!");
                return;
            }
            GenerateGraph();
        }
    }

    private void GenerateGraph() {
        // Cleanup existing waypoints
        ClearExistingWaypoints();

        List<Waypoint> spawnedWaypoints = new List<Waypoint>();
        GameObject parentObj = new GameObject("Waypoint_Container");

        int columns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));
        int rows = Mathf.CeilToInt((float)nodeCount / columns);
        float cellWidth = areaSize.x / columns;
        float cellHeight = areaSize.y / rows;

        for (int i = 0; i < nodeCount; i++) {
            int row = i / columns;
            int col = i % columns;

            float xPos = (-areaSize.x / 2) + (col * cellWidth) + (Random.Range(0.2f, 0.8f) * cellWidth);
            float zPos = (-areaSize.y / 2) + (row * cellHeight) + (Random.Range(0.2f, 0.8f) * cellHeight);
            Vector3 pos = new Vector3(xPos, 0, zPos);

            GameObject wpObj = (GameObject)PrefabUtility.InstantiatePrefab(waypointPrefab, parentObj.transform);
            wpObj.transform.position = pos;

            // Setup ID
            WaypointID idComponent = wpObj.GetComponent<WaypointID>();
            if (idComponent != null) {
                idComponent.waypointID = i;
                wpObj.name = $"Waypoint - {i}";
                EditorUtility.SetDirty(idComponent);
                EditorUtility.SetDirty(wpObj);
            }

            spawnedWaypoints.Add(wpObj.GetComponent<Waypoint>());
        }

        GenerateEdges(spawnedWaypoints);
        AssignToInputManager(spawnedWaypoints);

        SceneView.RepaintAll();
    }

    private void ClearExistingWaypoints() {
        GameObject container = GameObject.Find("Waypoint_Container");
        if (container != null) {
            DestroyImmediate(container);
        }

        Waypoint[] looseWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        foreach (var wp in looseWaypoints) {
            DestroyImmediate(wp.gameObject);
        }
    }

    private void GenerateEdges(List<Waypoint> waypoints) {
        int n = waypoints.Count;
        if (n < 2) return;

        // 1. GUARANTEE CONNECTIVITY & NO DEAD ENDS (Hamiltonian Cycle)
        // This ensures every node has at least 2 neighbors (in and out)
        for (int i = 0; i < n; i++) {
            Waypoint current = waypoints[i];
            Waypoint next = waypoints[(i + 1) % n]; // Loop back to start at the end

            if (!current.neighbors.Contains(next)) {
                current.neighbors.Add(next);
                next.neighbors.Add(current);
                EditorUtility.SetDirty(current);
                EditorUtility.SetDirty(next);
            }
        }

        // 2. ADDITIONAL DENSITY
        int maxPossiblePairs = (n * (n - 1)) / 2;
        int targetTwoWay = Mathf.FloorToInt(maxPossiblePairs * twoWayPercentage);
        int targetOneWay = Mathf.FloorToInt(maxPossiblePairs * oneWayPercentage);

        List<(int, int, float)> extraPairs = new List<(int, int, float)>();
        for (int i = 0; i < n; i++) {
            for (int j = i + 1; j < n; j++) {
                float dist = Vector3.Distance(waypoints[i].transform.position, waypoints[j].transform.position);
                // Only consider pairs within max distance that aren't already connected by the initial cycle
                if (dist <= maxConnectionDistance && !waypoints[i].neighbors.Contains(waypoints[j])) {
                    extraPairs.Add((i, j, dist));
                }
            }
        }

        var sortedPairs = extraPairs.OrderBy(p => p.Item3 * Random.Range(0.8f, 1.2f)).ToList();
        int pairIndex = 0;

        for (int i = 0; i < targetTwoWay && pairIndex < sortedPairs.Count; i++, pairIndex++) {
            var pair = sortedPairs[pairIndex];
            Waypoint wpA = waypoints[pair.Item1];
            Waypoint wpB = waypoints[pair.Item2];

            wpA.neighbors.Add(wpB);
            wpB.neighbors.Add(wpA);
            EditorUtility.SetDirty(wpA);
            EditorUtility.SetDirty(wpB);
        }

        for (int i = 0; i < targetOneWay && pairIndex < sortedPairs.Count; i++, pairIndex++) {
            var pair = sortedPairs[pairIndex];
            Waypoint from = Random.value > 0.5f ? waypoints[pair.Item1] : waypoints[pair.Item2];
            Waypoint to = (from == waypoints[pair.Item1]) ? waypoints[pair.Item2] : waypoints[pair.Item1];

            from.neighbors.Add(to);
            EditorUtility.SetDirty(from);
        }
    }

    private void AssignToInputManager(List<Waypoint> generatedWaypoints) {
        var manager = FindFirstObjectByType<BTInputManager>();

        if (manager == null) {
            Debug.LogWarning("Could not find a BTInputManager in the scene.");
            return;
        }

        int subsetCount = Mathf.Clamp(Mathf.FloorToInt(generatedWaypoints.Count * managerWaypointFillPercentage), 1, generatedWaypoints.Count);
        var selectedSubset = generatedWaypoints.OrderBy(x => Random.value).Take(subsetCount).ToList();

        SerializedObject soManager = new SerializedObject(manager);
        soManager.Update();

        SerializedProperty waypointsProp = soManager.FindProperty("waypoints");

        if (waypointsProp != null && waypointsProp.isArray) {
            waypointsProp.ClearArray();
            for (int i = 0; i < selectedSubset.Count; i++) {
                waypointsProp.InsertArrayElementAtIndex(i);
                waypointsProp.GetArrayElementAtIndex(i).objectReferenceValue = selectedSubset[i];
            }
        }

        SerializedProperty workersProp = soManager.FindProperty("workers");
        if (workersProp != null && workersProp.isArray) {
            for (int i = 0; i < workersProp.arraySize; i++) {
                var w = workersProp.GetArrayElementAtIndex(i).objectReferenceValue as MonoBehaviour;
                if (w != null) {
                    SerializedObject soWorker = new SerializedObject(w);
                    SerializedProperty startWpProp = soWorker.FindProperty("StartWaypoint");
                    if (startWpProp != null) {
                        startWpProp.objectReferenceValue = generatedWaypoints[Random.Range(0, generatedWaypoints.Count)];
                        soWorker.ApplyModifiedProperties();
                    }
                }
            }
        }

        soManager.ApplyModifiedProperties();
        Debug.Log($"Graph Generated: {nodeCount} nodes. No dead ends. Hierarchy names updated.");
    }
}