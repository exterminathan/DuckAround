using UnityEngine;
using System.Collections.Generic;

public class PrimaryBTBuilder {
    public static Node CreateTree() {
        return new Selector(new List<Node>
        {
            CollisionBTBuilder.BuildCollisionSequence(),
            CollisionBTBuilder.BuildRecoverySequence(),
            WaypointBTBuilder.BuildWaypointTraversal()
        }, "WorkerBehaviour");
    }
}
