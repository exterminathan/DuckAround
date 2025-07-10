using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Waypoint : MonoBehaviour {
    [Tooltip("Directly connected neighboring waypoints")]
    public List<Waypoint> neighbors = new List<Waypoint>();


    // draw connections in editor
    private void OnDrawGizmos() {
        Gizmos.color = Color.blue;
        foreach (var nb in neighbors) {
            if (nb != null)
                Gizmos.DrawLine(transform.position, nb.transform.position);
        }
        Gizmos.DrawSphere(transform.position, 0.2f);

    }
}
