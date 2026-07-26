using System.Collections.Generic;
using UnityEngine;

public static class RagdollChecks {
    // Set externally by WorkerPickupInteractable (via SetStateAtValue) while carried in the bill.
    public static bool IsHeldByPlayer(Dictionary<string, object> state) {
        return state.ContainsKey("IsHeld") && (bool)state["IsHeld"];
    }

    // Sphere, not vision cone — a downed worker senses a close player in any direction,
    // which keeps it playing dead until the player leaves. Centered on the PELVIS (the
    // actual body pile) — the root transform stays behind at the knockout spot when the
    // body is carried/flung elsewhere.
    public static bool IsPlayerInSuppressRange(Dictionary<string, object> state) {
        var ctrl = state["WorkerAIController"] as WorkerAIController;
        if (ctrl == null) return false;
        Vector3 center = ctrl.transform.position;
        if (state.ContainsKey("PelvisRigidbody") && state["PelvisRigidbody"] is Rigidbody pelvis) center = pelvis.position;
        return Physics.OverlapSphere(center, ctrl.RecoverySuppressRange, ctrl.PlayerDetectionLayerMask).Length > 0;
    }

    // Blocks recovery while the flung body is still in flight/sliding, so the worker
    // never stands up mid-air and the position reset runs on a stationary pile.
    public static bool IsRagdollSettled(Dictionary<string, object> state) {
        if (!state.ContainsKey("PelvisRigidbody")) return true;
        var rb = state["PelvisRigidbody"] as Rigidbody;
        var ctrl = state["WorkerAIController"] as WorkerAIController;
        if (rb == null || ctrl == null) return true;
        return rb.linearVelocity.magnitude <= ctrl.RecoverySettleSpeed;
    }
}
