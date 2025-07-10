using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class BTInputManager : MonoBehaviour {
    public WorkerAIController worker;
    public Waypoint[] waypoints;

    private int lastIndex;

    void Update() {
        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            var destination = Random.Range(0, waypoints.Length);
            while (destination == lastIndex)
                destination = Random.Range(0, waypoints.Length);
            Debug.Log($"Picked waypoint: {waypoints[destination].GetComponent<WaypointID>().waypointID}");
            lastIndex = destination;
            worker.SetNewDestination(waypoints[destination]);
        }

    }
}
