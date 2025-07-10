// using System.Collections.Generic;
// using UnityEngine;
// using static WaypointBTBuilder;
// using static BehaviourTreeNodes;

// [RequireComponent(typeof(Rigidbody))]
// public class WaypointRunner : BehaviourTreeRunner {
//     public List<Waypoint> destinations;
//     public float speed = 3f;
//     public float arriveThreshold = 0.1f;
//     private List<Waypoint> fullPath;

//     protected override void InitializeState() {
//         fullPath = new List<Waypoint>();
//         for (int i = 1; i < destinations.Count; i++) {
//             var segment = Pathfinding.FindPath(destinations[i - 1], destinations[i]);
//             if (segment == null) { Debug.LogError("No path!"); enabled = false; return; }
//             int start = (i == 1 ? 0 : 1);
//             for (int j = start; j < segment.Count; j++)
//                 fullPath.Add(segment[j]);
//         }

//         state["FullPath"] = fullPath;
//         state["PathIndex"] = 0;
//         state["SelfTransform"] = transform;
//         state["Speed"] = speed;
//         state["ArriveThreshold"] = arriveThreshold;
//     }

//     protected override Node BuildTree() {
//         return BuildTraversalSequence();
//     }
// }
