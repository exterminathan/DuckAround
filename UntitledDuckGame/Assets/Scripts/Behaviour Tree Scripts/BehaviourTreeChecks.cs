using System;
using System.Collections.Generic;
using UnityEngine;


public static class BehaviourTreeChecks {
    public static bool AlwaysTrue(Dictionary<string, object> state) => true;

    public static bool AlwaysFalse(Dictionary<string, object> state) => false;

    public static bool IsPlayerInRange(Dictionary<string, object> state) {
        if (state.TryGetValue("SelfTransform", out var selfObj) &&
            selfObj is Transform self &&
            state.TryGetValue("PlayerTransform", out var playerObj) &&
            playerObj is Transform player &&
            state.TryGetValue("PlayerSeenRange", out var rangeObj) &&
            rangeObj is float range) {
            return Vector3.Distance(self.position, player.position) <= range;
        }
        return false;
    }
}
