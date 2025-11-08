using UnityEngine;
using System.Collections.Generic;

public class DetectionChecks
{
    public static bool DetectPlayer(Dictionary<string, object> state) {
        var isRagdoll = state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
        var canMove = state.ContainsKey("IsAllowedToMove") && (bool)state["IsAllowedToMove"];

        if (isRagdoll || !canMove) {
            state["PlayerTransform"] = null;
            return false;
        }

        var ctrl = (WorkerAIController)state["WorkerAIController"];
        var range = GlobalAlarm.GetCurrentLevelData().playerDetectionDistance;
        var angle = GlobalAlarm.GetCurrentLevelData().playerDetectionAngle;

        var hits = Physics.OverlapSphere(ctrl.transform.position, range, ctrl.PlayerDetectionLayerMask);
        //check if hit is in detection range
        bool seen = hits.Length > 0 && Vector3.Angle(ctrl.transform.forward, (hits[0].transform.position - ctrl.transform.position).normalized) <= angle * 0.5f;
        //draw debug lines on edges of angle of detection range
        Vector3 rightDir = Quaternion.Euler(0, angle * 0.5f, 0) * ctrl.transform.forward;
        Vector3 leftDir = Quaternion.Euler(0, -angle * 0.5f, 0) * ctrl.transform.forward;
        Debug.DrawLine(ctrl.transform.position, ctrl.transform.position + (rightDir * range), Color.blue, 0.01f);
        Debug.DrawLine(ctrl.transform.position, ctrl.transform.position + (leftDir * range), Color.blue, 0.01f);
        Debug.DrawLine(ctrl.transform.position, ctrl.transform.position + (ctrl.transform.forward * range), Color.red, 0.01f);

        state["PlayerTransform"] = seen ? hits[0].transform : null;

        if (seen) {
            state["LastDetectionTime"] = Time.time;

            // set detection visual to yellow
            ctrl.WorkerVisController.SetVisualColor(StateName.ALERT);

        }
        return seen;
    }

    public static bool IsChasing(Dictionary<string, object> state)
        => state.ContainsKey("IsChasing") && (bool)state["IsChasing"];
    
    public static bool LostPlayer(Dictionary<string, object> state) {
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        float last = (float)state["LastDetectionTime"];
        float chaseDuration = GlobalAlarm.GetCurrentLevelData().chaseTimer;

        bool endCond = Time.time - last > chaseDuration;
        if (endCond) {
            ctrl.WorkerVisController.SetVisualColor(StateName.PATROL);
        }

        return endCond;
    }
}
