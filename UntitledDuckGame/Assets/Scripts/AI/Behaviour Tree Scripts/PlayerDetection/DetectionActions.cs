using UnityEngine;
using System.Collections.Generic;

public static class DetectionActions {
    private static float chaseReAcquireCooldown = .5f;
    public static bool BeginChase(Dictionary<string, object> state) {
        Debug.Log("[DetectionActions] BeginChase called.");
        //check if ragdoll or can't move
        var ctrl = (WorkerAIController)state["WorkerAIController"];

        var isRagdoll = state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
        var canMove = state.ContainsKey("IsAllowedToMove") && (bool)state["IsAllowedToMove"];

        if (isRagdoll || !canMove) {
            state["PlayerTransform"] = null;
            return false;
        }

        float lastSeen = state.ContainsKey("LastDetectionTime") ? (float)state["LastDetectionTime"] : 0f;

        Debug.Log("Time since last seen: " + (Time.time - lastSeen));

        GlobalAlarm.RequestIncrease(1f);
        //alert animation when beginning chase
        ctrl.SetAlertAnimationActive(true);


        state["IsChasing"] = true;
        state.Remove("FullPath");

        return true;
    }

    public static bool ChasePlayerTick(Dictionary<string, object> state) {
        //Debug.Log("[DetectionActions] ChasePlayerTick called.");
        var isRagdoll = state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
        var canMove = state.ContainsKey("IsAllowedToMove") && (bool)state["IsAllowedToMove"];
        if (isRagdoll || !canMove) {
            state["PlayerTransform"] = null;
            return false;
        }

        var ctrl = (WorkerAIController)state["WorkerAIController"];
        var self = ctrl.transform;
        var player = (Transform)state["PlayerTransform"];
        var chaseSpeed = GlobalAlarm.GetCurrentLevelData().playerChaseSpeed;

        var animator = (Animator)state["WorkerAnimator"];

        if (player == null) return false;

        Vector3 dir = player.position - self.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) {
                self.rotation = Quaternion.LookRotation(dir);
            self.position += chaseSpeed * self.forward * ctrl.MoveSpeed * Time.deltaTime;

            //change animator speed 
            animator.speed = chaseSpeed;
            }


        return true;
    }

    public static bool EndChase(Dictionary<string, object> state) {
        Debug.Log("[DetectionActions] EndChase called.");
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        state["IsChasing"] = false;

        //hide alert animation when chase is over
        ctrl.SetAlertAnimationActive(false);

        return true;
    }
    
    public static bool ResetPlayerUponRagdoll(Dictionary<string, object> state) {
        Debug.Log("[DetectionActions] ResetPlayerUponRagdoll called.");
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        state["IsChasing"] = false;
        state["LastDetectionTime"] = 0f;
        state["PlayerTransform"] = null;

        state["IsAllowedToMove"] = false;
        state.Remove("FullPath");

        return true;
    }

}
