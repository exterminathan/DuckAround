using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class CollisionChecks {
    public static bool HasCollision(Dictionary<string, object> state) {
        return state.ContainsKey("IsCollided") && (bool)state["IsCollided"];
    }

    public static bool IsRagdollActive(Dictionary<string, object> state) {
        return state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
    }

    public static bool HasStopTimeElapsed(Dictionary<string, object> state) {
        if (!state.ContainsKey("CollisionStartTime")) return false;
        float t0 = (float)state["CollisionStartTime"];
        return Time.time - t0 >= 3f;
    }
}
