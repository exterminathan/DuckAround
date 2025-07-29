using UnityEngine;
public class ArmHitForwarder : MonoBehaviour {
    private void OnTriggerEnter(Collider other) {
        //Collisions with npc
        var npc = other.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);


    }
}

