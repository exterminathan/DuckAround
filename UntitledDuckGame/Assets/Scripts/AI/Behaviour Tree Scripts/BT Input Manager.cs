using UnityEngine;

public class BTInputManager : MonoBehaviour {
    public WorkerAIController[] workers;
    public Waypoint[] waypoints;
    private int lastIndex;

    public void SetNewDestinations(WorkerAIController[] targets = null) 
    {
        WorkerAIController[] workersToCommand = (targets == null) ? workers : targets;

        foreach (var worker in workersToCommand) 
        {
            if (waypoints.Length == 0) return;

            var destination = Random.Range(0, waypoints.Length);
            while (destination == lastIndex && waypoints.Length > 1)
                destination = Random.Range(0, waypoints.Length);
            
            lastIndex = destination;
            worker.SetNewDestination(waypoints[destination]);
        }
    }
}