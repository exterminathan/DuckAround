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

        if (target == null || self == null) return false;
        return Vector3.Distance(self.position, target.transform.position) <= threshold;
    }
}
