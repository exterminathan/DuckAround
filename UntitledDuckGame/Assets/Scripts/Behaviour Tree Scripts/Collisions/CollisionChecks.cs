using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class CollisionChecks {
    public static bool HasCollision(Dictionary<string, object> state) {
        //Debug.Log("Checking for collision: " + (state.ContainsKey("IsCollided") ? state["IsCollided"].ToString() : "Key not found"));
        return state.ContainsKey("IsCollided") && (bool)state["IsCollided"];
    }

    public static bool IsRagdollActive(Dictionary<string, object> state) {
        Debug.Log("Checking if ragdoll is active: " + (state.ContainsKey("IsRagdollActive") ? state["IsRagdollActive"].ToString() : "Key not found"));
        return state.ContainsKey("IsRagdollActive") && (bool)state["IsRagdollActive"];
    }

    public static bool HasStopTimeElapsed(Dictionary<string, object> state) {
        if (!state.ContainsKey("CollisionStartTime")) return false;
        float t0 = (float)state["CollisionStartTime"];
        Debug.Log($"CollisionStartTime: {t0}, Current Time: {Time.time}, Elapsed: {Time.time - t0}");
        Debug.Log($"HasStopTimeElapsed: {Time.time - t0 >= 3f}");
        return Time.time - t0 >= 3f;
    }
}
