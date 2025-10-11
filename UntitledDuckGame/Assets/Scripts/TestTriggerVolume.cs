using UnityEngine;

public class TestTriggerVolume : MonoBehaviour {
    [SerializeField] Waypoint loopStart;
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    private void OnTriggerEnter(Collider other) {
        var npc = other.GetComponent<WorkerAIController>();
        if (npc != null) {
            Debug.Log($"Trigger Entered by {other.name}");
            npc.SetNewDestination(loopStart);
        }
    }
}
