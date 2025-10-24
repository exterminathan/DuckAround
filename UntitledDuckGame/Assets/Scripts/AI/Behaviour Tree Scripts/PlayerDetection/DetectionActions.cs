using UnityEngine;
using System.Collections.Generic;
public static class DetectionActions {
    private static float chaseReAcquireCooldown = .5f;
    public static bool BeginChase(Dictionary<string, object> state) {
        Debug.Log("[DetectionActions] BeginChase called.");

        float lastSeen = state.ContainsKey("LastSeenTime") ? (float)state["LastSeenTime"] : 0f;

        Debug.Log("Time since last seen: " + (Time.time - lastSeen));

        if (Time.time - lastSeen > chaseReAcquireCooldown) {

            GlobalAlarm.Add(1f);
        }

        state["IsChasing"] = true;
        state.Remove("FullPath");

        return true;
    }

    public static bool ChasePlayerTick(Dictionary<string, object> state) {
        //Debug.Log("[DetectionActions] ChasePlayerTick called.");

        var ctrl = (WorkerAIController)state["WorkerAIController"];
        var self = ctrl.transform;
        var player = (Transform)state["PlayerTransform"];
        var speed = (float)state["ChaseSpeed"];

        if (player == null) return false;

        //walk in direction of player, slow rn 
        //use navmesh here? TODO
        Vector3 dir = (player.position - self.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) {
                self.rotation = Quaternion.LookRotation(dir);
                self.position += speed * self.forward * ctrl.MoveSpeed * Time.deltaTime;
            }


        return true;
    }
    
    public static  bool EndChase(Dictionary<string, object> state) {
        Debug.Log("[DetectionActions] EndChase called.");
        state["IsChasing"] = false;

        return true;
    }

}
