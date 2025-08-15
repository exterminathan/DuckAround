using System.Collections.Generic;

public static class WaypointBTBuilder {
    public static Node BuildWaypointTraversal() {
        return new Sequence(new List<Node>
        {
            new CheckNode(WaypointChecks.IsAllowedToMoveCheck),

            new Selector(new List<Node>
            {
                new Sequence(new List<Node>
                {
                    new CheckNode(WaypointChecks.IsAtTargetWaypoint),
                    new ActionNode(AnimationActions.SetIdle),
                    new CheckNode(AnimationChecks.IsInIdleState)
                }, "IdleAtTarget"),

                new Sequence(new List<Node>
                {
                    new Selector(new List<Node>
                    {
                        new CheckNode(WaypointChecks.HasPathCheck),
                        new ActionNode(WaypointActions.FindPathAction)
                    }, "EnsurePath"),

                    new ActionNode(AnimationActions.SetWalking),
                    new CheckNode(AnimationChecks.IsInWalkingState),

                    new Selector(new List<Node>
                    {
                        new Sequence(new List<Node>
                        {
                            new ActionNode(WaypointActions.PathTraversalAction),
                            new ActionNode(AnimationActions.SetIdle),
                            new CheckNode(AnimationChecks.IsInIdleState)
                        }, "FinishTraversalAndIdle"),

                        new ActionNode(WaypointActions.PathTraversalAction)
                    })
                }, "DoTravel")
            })
        }, "WaypointTraversal");
    }

    public static Node CreateTree() {
        return new Selector(new List<Node>
        {
            BuildWaypointTraversal()
        });
    }
}
