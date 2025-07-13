using System.Collections.Generic;

public static class CollisionBTBuilder {
    public static Node BuildCollisionSequence() {
        return new Sequence(new List<Node>
        {
            new CheckNode(CollisionChecks.HasCollision, "CheckCollision"),
            new ActionNode(AnimationActions.SetIdle, "SetIdle"),
            new ActionNode(CollisionActions.HandleCollision, "HandleCollision")
        }, "CollisionHandler");
    }

    public static Node BuildRecoverySequence() {
        return new Sequence(new List<Node>
        {
            new CheckNode(CollisionChecks.HasStopTimeElapsed,   "CheckStopElapsed"),
            new CheckNode(CollisionChecks.IsRagdollActive, "CheckRagdollActive"),
            new ActionNode(AnimationActions.SetWalking, "SetWalking"),
            new ActionNode(CollisionActions.RecoverFromCollision, "RecoverFromCollision")
        }, "RecoverHandler");
    }
}
