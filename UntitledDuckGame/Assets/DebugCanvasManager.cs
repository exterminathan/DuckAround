using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class DebugCanvasManager : MonoBehaviour {
    public BTInputManager btInput;
    public LayerMask workerLayer;
    public LayerMask groundLayer;
    public GameObject workerPanelPrefab;
    public GameObject dropCubePrefab;
    public Vector3 uiOffset = new Vector3(0, 2f, 0);

    private Dictionary<WorkerAIController, GameObject> activeSelections = new Dictionary<WorkerAIController, GameObject>();
    private Camera mainCam;

    void Start() {
        mainCam = Camera.main;
        if (btInput == null) Debug.LogError("BTInputManager reference is missing.");
    }

    void Update() {
        if (Input.GetMouseButtonDown(1)) {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            HandleSelection();
        }
        if (Input.GetMouseButtonDown(0)) {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            SpawnCube();
        }

        UpdatePanels();
    }

    void HandleSelection() {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, workerLayer)) {
            var worker = hit.transform.GetComponent<WorkerAIController>();
            if (worker != null) ToggleSelection(worker);
        }
    }

    void SpawnCube() {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer)) {
            Instantiate(dropCubePrefab, hit.point + Vector3.up * 0.5f, Quaternion.identity);
        }
    }

    void ToggleSelection(WorkerAIController worker) {
        if (activeSelections.ContainsKey(worker)) {
            Destroy(activeSelections[worker]);
            activeSelections.Remove(worker);
        }
        else {
            GameObject panel = Instantiate(workerPanelPrefab, transform);
            activeSelections.Add(worker, panel);
        }
    }

    void UpdatePanels() {
        List<WorkerAIController> toRemove = new List<WorkerAIController>();

        foreach (var entry in activeSelections) {
            if (entry.Key == null) {
                toRemove.Add(entry.Key);
                continue;
            }

            // world to screen translation
            entry.Value.transform.position = mainCam.WorldToScreenPoint(entry.Key.transform.position + uiOffset);

            var t = entry.Value.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = entry.Key.GetData();
        }

        foreach (var worker in toRemove) {
            activeSelections.Remove(worker);
        }
    }

    public void OnCommandSelectedWorkers() {
        if (activeSelections.Count == 0) return;

        WorkerAIController[] selected = new List<WorkerAIController>(activeSelections.Keys).ToArray();
        btInput.SetNewDestinations(selected);
    }
}