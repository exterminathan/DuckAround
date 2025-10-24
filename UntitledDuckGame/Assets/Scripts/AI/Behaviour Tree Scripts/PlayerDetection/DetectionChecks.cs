using UnityEngine;
using System.Collections.Generic;

public class DetectionChecks
{
    public static bool DetectPlayer(Dictionary<string, object> state) {
        //Debug.Log("[DetectionChecks] DetectPlayer called.");
        var ctrl = (WorkerAIController)state["WorkerAIController"];
        var range = (float)state["PlayerDetectionRange"];

        var hits = Physics.OverlapSphere(ctrl.transform.position, range, ctrl.PlayerDetectionLayerMask);
        //check if hit is in front 90 degrees of AI
        bool seen = hits.Length > 0 && Vector3.Dot(ctrl.transform.forward, (hits[0].transform.position - ctrl.transform.position).normalized) > 0f;
        Debug.DrawLine(ctrl.transform.position, ctrl.transform.position + (ctrl.transform.forward * range), seen ? Color.red : Color.green, 0.1f);

        state["PlayerTransform"] = seen ? hits[0].transform : null;

        if (seen) state["LastSeenTime"] = Time.time;
        return seen;
    }

    public static bool IsChasing(Dictionary<string, object> state)
        => state.ContainsKey("IsChasing") && (bool)state["IsChasing"];
    
    public static bool LostPlayer(Dictionary<string, object> state) {
        //Debug.Log("[DetectionChecks] LostPlayer called.");
        float last = (float)state["LastSeenTime"];
        float chaseDuration = (float)state["PlayerChaseTimer"];
        //Debug.Log("Time left: " + (Time.time - last) + " / " + chaseDuration);


        return Time.time - last > chaseDuration;
    }
}
