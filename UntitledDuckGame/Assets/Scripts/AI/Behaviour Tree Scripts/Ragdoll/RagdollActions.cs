using System.Collections.Generic;
using UnityEngine;
using System;

public static class RagdollActions {
    // turns ragdoll on + plays sound
    public static bool EnterRagdoll(Dictionary<string, object> state) {
        var ctrl = state["WorkerAIController"] as WorkerAIController;
        ctrl.ApplyRagdoll(true);
        AudioManager.instance.PlayOneShot(FMODEvents.instance.ragdollSound, ctrl.transform.position);
        return true;
    }

    // turns ragdoll off
    public static bool ExitRagdoll(Dictionary<string, object> state) {
        var ctrl = state["WorkerAIController"] as WorkerAIController;
        ctrl.ApplyRagdoll(false);
        return true;
    }

    public static bool ResetWorkerPositionAfterRagdoll(Dictionary<string, object> state) {
        var ctrl = state["WorkerAIController"] as WorkerAIController;
        Dictionary<Type, Collider[]> colliders = ctrl.rigidbodyColliders;

        //get avg 2d position of the colliders - center
        Vector3 sum = Vector3.zero;
        int ct = 0;

        foreach (var kvp in colliders) {
            foreach (var col in kvp.Value) {
                Debug.Log($"val: {col.bounds.center}");
                sum += col.bounds.center;
                ct++;
            }
        }
        Debug.Log($"Count: {ct}");

        Vector3 avg = sum / ct;
        Debug.Log($"Avg: {avg}");

        // move player to that position
        Vector3 newPos = new Vector3(avg.x, ctrl.transform.position.y, avg.z);

        ctrl.transform.position = newPos;


        return true;
    }
}
