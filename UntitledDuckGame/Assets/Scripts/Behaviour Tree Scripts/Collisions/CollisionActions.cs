using System.Collections.Generic;
using UnityEngine;

public static class CollisionActions {
    public static bool HandleCollision(Dictionary<string, object> state) {
        state["IsAllowedToMove"] = false;
        state["IsRagdollActive"] = true;
        state["IsCollided"] = false;
        state["CollisionStartTime"] = Time.time;

        return true;
    }

    public static bool RecoverFromCollision(Dictionary<string, object> state) {
        state["IsAllowedToMove"] = true;

        state["IsRagdollActive"] = false;

        state.Remove("CollisionStartTime");

        Debug.Log("Recovering");
        return true;
    }
}
