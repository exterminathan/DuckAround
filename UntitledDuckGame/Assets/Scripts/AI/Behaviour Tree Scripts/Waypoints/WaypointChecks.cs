using System.Collections.Generic;
using UnityEngine;

public static class WaypointChecks {
    public static bool HasPathCheck(Dictionary<string, object> state) {
        return state.ContainsKey("FullPath");
    }

    public static bool HasMoreWaypointsCheck(Dictionary<string, object> state) {
        var path = state.GetValueOrDefault("FullPath") as List<Waypoint>;
        int index = (int)state.GetValueOrDefault("PathIndex");
        return (path != null && index < path.Count);
    }

    public static bool IsAllowedToMoveCheck(Dictionary<string, object> state) {
        return state.ContainsKey("IsAllowedToMove") && (bool)state["IsAllowedToMove"];
    }

    public static bool IsAtTargetWaypoint(Dictionary<string, object> state) {
        var target = state.GetValueOrDefault("TargetWaypoint") as Waypoint;
        var self = state.GetValueOrDefault("SelfTransform") as Transform;
        var threshold = (float)state.GetValueOrDefault("ArriveThreshold");
        var ctrl = state.GetValueOrDefault("WorkerAIController") as WorkerAIController;

        if (target == null || self == null) return false;
        bool isAt = Vector3.Distance(self.position, target.transform.position) <= threshold;
        
        if (isAt) {
            ctrl.WorkerVisController.SetVisualColor(StateName.IDLE);
            ctrl.WorkerVisController.SetVisualParameters(20f, 1f);

            //temp
            //rotate towards player
            self.LookAt(new Vector3(ctrl.PlayerTransform.position.x, self.position.y, ctrl.PlayerTransform.position.z));
        }
        
        return isAt;
    }

    public static bool HasTargetWaypoint(Dictionary<string, object> state) {
        var target = state.GetValueOrDefault("TargetWaypoint") as Waypoint;

        Debug.Log($"{((target == null) ? "null" : "nah")}");
        return (target != null);
    }
}
