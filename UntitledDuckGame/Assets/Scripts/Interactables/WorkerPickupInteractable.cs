using UnityEngine;

// Runtime-added to the worker's pelvis bone by WorkerAIController so a ragdolled worker
// can be carried by the midriff: the pelvis rides kinematic in the bill while the jointed
// limbs keep dangling. Talks to the worker BT only through SetStateAtValue ("IsHeld",
// "CollisionStartTime") — never the blackboard directly.
public class WorkerPickupInteractable : PickupInteractable {
    [HideInInspector] public WorkerAIController Controller;
    [HideInInspector] public Rigidbody[] BoneRigidbodies;
    [HideInInspector] public bool PickupAllowed; // toggled by ApplyRagdoll — grabbable only while down

    private float totalMass = -1f;

    // Whole dangling body, not just the pelvis bone — drives encumbrance + fling falloff.
    public override float CarryMass {
        get {
            if (totalMass < 0f && BoneRigidbodies != null) {
                totalMass = 0f;
                foreach (var rb in BoneRigidbodies) {
                    if (rb != null) totalMass += rb.mass;
                }
            }
            return totalMass > 0f ? totalMass : base.CarryMass;
        }
    }

    // Ragdoll rides the joint-hung carry path so the limbs keep flailing.
    public override bool DanglingCarry => true;

    // Bite stiffness knobs live on the controller so they're tunable in its inspector.
    public override float DanglingRotationSpring => Controller != null ? Controller.CarryRotationSpring : 0f;
    public override float DanglingRotationDamper => Controller != null ? Controller.CarryRotationDamper : 0f;

    // No ItemEvents in either direction — a worker is not a score/conveyor item.
    public override void OnHoldStart(RaycastHit hit, IsometricRaycaster arm) {
        if (!PickupAllowed) return; // click landed on the recovery frame

        // re-read carry tuning so inspector tweaks on the controller apply per-grab in play mode
        if (Controller != null) {
            gripSize = Controller.CarryGripSize;
            gripOffset = Controller.CarryGripOffset;
            gripRotation = Controller.CarryGripRotation;
        }

        pickupActive = true;
        holder = HeldItemController.GetOrAdd(arm);
        holder.Grab(this);
        if (Controller != null) Controller.SetStateAtValue("IsHeld", true);
    }

    public override void OnHoldEnd() {
        pickupActive = false;

        if (holder != null) holder.Release();
        holder = null;
    }

    // Runs on EVERY release path (fling and gentle drop alike), so IsHeld can never
    // stay stuck true and the recovery timer always restarts from the release moment.
    public override void OnFlung(Vector3 flingVelocity) {
        if (BoneRigidbodies != null) {
            foreach (var rb in BoneRigidbodies) {
                if (rb != null && rb != Body && !rb.isKinematic) rb.linearVelocity = flingVelocity;
            }
        }
        if (Controller != null) {
            Controller.SetStateAtValue("IsHeld", false);
            Controller.SetStateAtValue("CollisionStartTime", Time.time);
        }
    }

    // ragdoll bodies thud constantly — no ItemEvents impact spam
    protected override void OnCollisionEnter(Collision collision) { }
}
