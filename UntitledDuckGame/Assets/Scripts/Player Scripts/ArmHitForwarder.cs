using UnityEngine;
public class ArmHitForwarder : MonoBehaviour {
    private void OnTriggerEnter(Collider other) {
        var npc = other.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);
    }
}

