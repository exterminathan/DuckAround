using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public static class DetectionActions {
    private static float chaseReAcquireCooldown = .5f;
    public static bool BeginChase(Dictionary<string, object> state) {
        //check if ragdoll or can't move
        var ctrl = (WorkerAIController)state["WorkerAIController"];

        //make sure worker isn't ragdolled
        var isRagdoll = state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
        var canMove = state.ContainsKey("IsAllowedToMove") && (bool)state["IsAllowedToMove"];

        if (isRagdoll || !canMove) {
            state["PlayerTransform"] = null;
            return false;
        }

        //increase global alarm
        GlobalAlarm.RequestIncrease(1f);

        //alert animation when beginning chase
        ctrl.SetAlertAnimationActive(true);

        //workervisual color and parameter updates
        ctrl.WorkerVisController.SetVisualColor(StateName.CHASING);

        var range = GlobalAlarm.GetCurrentLevelData().playerDetectionDistance;
        var angle = GlobalAlarm.GetCurrentLevelData().playerDetectionAngle;
        ctrl.WorkerVisController.SetVisualParameters(angle, range);

        //set chasing state and remove waypoint path
        state["IsChasing"] = true;
        state.Remove("FullPath");

        return true;
    }

    public static bool ChasePlayerTick(Dictionary<string, object> state) {
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
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        state["IsChasing"] = false;

        //reset animator speed
        var animator = (Animator)state["WorkerAnimator"];
        animator.speed = 1f;

        //hide alert animation when chase is over
        ctrl.SetAlertAnimationActive(false);

        //workervisual color and parameter updates
        ctrl.WorkerVisController.SetVisualColor(StateName.PATROL);

        ctrl.WorkerVisController.SetVisualParametersToDefault();

        return true;
    }
    
    public static bool ResetPlayerUponRagdoll(Dictionary<string, object> state) {
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        state["IsChasing"] = false;
        state["LastDetectionTime"] = 0f;
        state["PlayerTransform"] = null;

        state["IsAllowedToMove"] = false;
        state.Remove("FullPath");

        ctrl.WorkerVisController.SetVisualColor(StateName.PATROL, true);
        ctrl.WorkerVisController.SetVisualParametersToDefault();


        return true;
    }

}
