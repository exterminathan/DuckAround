// Pathfinding.cs
using System.Collections.Generic;
using UnityEngine;

public static class Pathfinding {
    // BFS on an unweighted graph
    public static List<Waypoint> FindPath(Waypoint start, Waypoint goal) {
        if (start == null || goal == null) return null;
        var queue = new Queue<Waypoint>();
        var cameFrom = new Dictionary<Waypoint, Waypoint>();
        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (current == goal) break;
            foreach (var nb in current.neighbors) {
                if (nb != null && !cameFrom.ContainsKey(nb)) {
                    queue.Enqueue(nb);
                    cameFrom[nb] = current;
                }
            }
        }

        // reconstruct path
        if (!cameFrom.ContainsKey(goal)) return null;
        var path = new List<Waypoint>();
        var node = goal;
        while (node != null) {
            path.Add(node);
            node = cameFrom[node];
        }
        path.Reverse();
        Debug.Log($"Path found from {start.name} to {goal.name} with {path.Count} waypoints.");
        return path;
    }
}
