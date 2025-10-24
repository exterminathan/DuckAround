// DetectionBTBuilder.cs
using System.Collections.Generic;

public static class DetectionBTBuilder {
    public static Node BuildDetectionBranch() {
        return new Selector(new List<Node> {

            // If already chasing: either keep chasing or end chase if lost
            new Sequence(new List<Node> {
                new CheckNode(DetectionChecks.IsChasing, "IsChasing"),
                new Selector(new List<Node> {
                    new Sequence(new List<Node> {
                        new CheckNode(DetectionChecks.LostPlayer, "LostPlayer"),
                        new ActionNode(DetectionActions.EndChase, "EndChase")
                    }, "HandleLost"),

                    new Sequence(new List<Node> {
                        new ActionNode(DetectionActions.ChasePlayerTick, "ChaseTick"),
                        new ActionNode(AnimationActions.SetWalking, "SetWalking"),
                        new CheckNode(AnimationChecks.IsInWalkingState, "IsWalking")
                    }, "KeepChasing")
                })
            }, "ChaseBranch"),

            // Otherwise: opportunistically start chase if we see the player
            new Sequence(new List<Node> {
                new CheckNode(DetectionChecks.DetectPlayer, "DetectPlayer"),
                new ActionNode(DetectionActions.BeginChase, "BeginChase")
            }, "AcquireBranch")

        }, "Detection");
    }
}
