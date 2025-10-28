using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class WaypointActions {
    public static bool FindPathAction(Dictionary<string, object> state) {
        var ctrl = state.GetValueOrDefault("WorkerAIController") as WorkerAIController;
        var start = ctrl.FindClosestWaypoint(ctrl.transform.position);
        var target = state.GetValueOrDefault("TargetWaypoint") as Waypoint;
        if (start == null || target == null) return false;

        List<Waypoint> path = Pathfinding.FindPath(start, target);
        if (path == null || path.Count == 0) return false;

        state["FullPath"] = path;
        state["PathIndex"] = 0;

        string pathStr = string.Join(" → ", path.Select(wp => wp.name[^1]));
        Debug.Log($"[BT] Target Found → {target.name[^1]} (Path: {pathStr})");
        return true;
    }

    public static bool PathTraversalAction(Dictionary<string, object> state) {
        var path = state.GetValueOrDefault("FullPath") as List<Waypoint>;
        int index = state.GetValueOrDefault("PathIndex") is int i ? i : 0;
        var selfTrans = state.GetValueOrDefault("SelfTransform") as Transform;
        float speed = state.GetValueOrDefault("Speed") is float s ? s : 0f;
        float thresh = state.GetValueOrDefault("ArriveThreshold") is float t ? t : 0.1f;
        var ctrl = state.GetValueOrDefault("WorkerAIController") as WorkerAIController;
        var self = state.GetValueOrDefault("SelfTransform") as Transform;
        Animator anim = state.GetValueOrDefault("WorkerAnimator") as Animator;

        if (path == null || selfTrans == null)
            return true;

        // if finished
        if (index >= path.Count) {
            state.Remove("FullPath");
            if (state.ContainsKey("TargetWaypoint")) {
                state["StartWaypoint"] = state["TargetWaypoint"];
                state["TargetWaypoint"] = null;
            }
            //Debug.Log("[BT] Finished entire path; at target now");
            anim.SetBool("isWalking", false);
            Debug.Log("Animator isWalking set to false upon finishing path");

            //Debug.Log("Reset path details upon finishing path");

            ctrl.WorkerVisController.SetVisualColor(StateName.IDLE);
            ctrl.WorkerVisController.SetVisualParameters(20f, 1f);

            //temp
            //rotate towards player
            self.LookAt(new Vector3(ctrl.PlayerTransform.position.x, self.position.y, ctrl.PlayerTransform.position.z));

            return true;
        }

        // Next waypoint logic
        Waypoint targetWP = path[index];
        Vector3 dir = targetWP.transform.position - selfTrans.position;
        dir.y = 0;

        if (dir.sqrMagnitude > .0005f) {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            selfTrans.rotation = Quaternion.Slerp(selfTrans.rotation, targetRotation, .25f);
        }

        selfTrans.position = Vector3.MoveTowards(selfTrans.position, targetWP.transform.position,
                                                 speed * Time.deltaTime);

        if (Vector3.Distance(selfTrans.position, targetWP.transform.position) <= thresh) {
            state["PathIndex"] = index + 1;
            //Debug.Log($"[BT] Arrived at waypoint {index}, advancing to {index + 1}");
        }

        return false;
    }
}
