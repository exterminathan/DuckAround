using System.Collections.Generic;
using UnityEngine;

public static class RotationActions {
    public static bool RotateToNextWaypointAction(Dictionary<string, object> state) {
        var path = state.GetValueOrDefault("FullPath") as List<Waypoint>;
        int index = (int)state.GetValueOrDefault("PathIndex");
        var selfTrans = state.GetValueOrDefault("SelfTransform") as Transform;

        if (path == null || selfTrans == null || index >= path.Count)
            return false;

        Vector3 dir = path[index].transform.position - selfTrans.position;
        dir.y = 0;
        if (dir.sqrMagnitude < Mathf.Epsilon)
            return true;

        selfTrans.rotation = Quaternion.LookRotation(dir);
        Debug.Log($"[BT] Rotated to face waypoint {index}");
        return true;
    }
}
