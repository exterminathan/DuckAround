using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class Waypoint : MonoBehaviour {
    [Tooltip("Directly connected neighboring waypoints")]
    public List<Waypoint> neighbors = new List<Waypoint>();


    // draw connections in editor
    private void OnDrawGizmos() {
        foreach (var nb in neighbors) {
            if (nb != null) {
                if (nb.neighbors.Contains<Waypoint>(this)) {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(transform.position, nb.transform.position);
                }
                else {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(transform.position, nb.transform.position);
                }

            }

        }
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 0.2f);

    }
}
