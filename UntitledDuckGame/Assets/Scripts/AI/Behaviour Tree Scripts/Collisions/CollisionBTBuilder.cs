using System.Collections.Generic;

public static class CollisionBTBuilder {
    public static Node BuildCollisionSequence() {
        return new Sequence(new List<Node>
        {
            new CheckNode(CollisionChecks.HasCollision, "CheckCollision"),
            //while carried in the bill, never re-enter ragdoll (would un-kinematic the pelvis mid-carry)
            new Inverter(new CheckNode(RagdollChecks.IsHeldByPlayer, "IsHeld"), "NotHeld"),
            new ActionNode(AnimationActions.SetIdle, "SetIdle"),
            new ActionNode(CollisionActions.HandleCollision, "HandleCollision"),
            new ActionNode(DetectionActions.ResetPlayerUponRagdoll, "reset player if ragdoll"),
            new ActionNode(RagdollActions.EnterRagdoll, "EnterRagdoll")
        }, "CollisionHandler");
    }

    public static Node BuildRecoverySequence() {
        return new Sequence(new List<Node>
        {
            new CheckNode(CollisionChecks.HasStopTimeElapsed,   "CheckStopElapsed"),
            new CheckNode(CollisionChecks.IsRagdollActive, "CheckRagdollActive"),
            //stay down while carried, while the player lingers close, or while still in flight
            new Inverter(new CheckNode(RagdollChecks.IsHeldByPlayer, "IsHeld"), "NotHeldForRecovery"),
            new Inverter(new CheckNode(RagdollChecks.IsPlayerInSuppressRange, "PlayerNear"), "PlayerNotNear"),
            new CheckNode(RagdollChecks.IsRagdollSettled, "CheckSettled"),
            new ActionNode(RagdollActions.ExitRagdoll, "ExitRagdoll"),
            new ActionNode(RagdollActions.ResetWorkerPositionAfterRagdoll, "ResetWorkerPosition"),
            new ActionNode(CollisionActions.RecoverFromCollision, "RecoverFromCollision"),

            //if no target, set idle
            new Sequence(new List<Node> {
                new CheckNode(WaypointChecks.HasTargetWaypoint, "CheckHasTargetWaypoint"),
                new ActionNode(AnimationActions.SetWalking, "SetWalking"),
            }, "CheckNoTargetIdleAfterRecovery"),

        }, "RecoverHandler");
    }
}
