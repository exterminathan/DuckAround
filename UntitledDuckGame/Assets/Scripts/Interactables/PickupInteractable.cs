using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;
    public bool pickupActive;

    [Header("Grip Tuning")]
    [Tooltip("Item size used for the mouth gape. 0 = measured from collider bounds.")]
    public float gripSize = 0f;
    [Tooltip("Local offset from the hold slot while carried.")]
    public Vector3 gripOffset = Vector3.zero;
    [Tooltip("Extra rotation (euler) applied on top of the hold-slot rotation while carried.")]
    public Vector3 gripRotation = Vector3.zero;

    [Header("Impact Reporting")]
    [Tooltip("Minimum collision speed (m/s) that counts as an ItemEvents impact.")]
    public float minImpactSpeed = 0.5f;
    [Tooltip("Seconds between reported impacts, so rolling/sliding contact doesn't spam.")]
    public float minImpactInterval = 0.1f;
    private float nextImpactTime;

    private ConveyorObjectMover conveyorObjectMover;
    protected HeldItemController holder;

    public ConveyorObjectMover ConveyorMover => conveyorObjectMover;
    public Rigidbody Body { get; private set; }

    // Effective carried mass for encumbrance + fling falloff; multi-body carriers override.
    public virtual float CarryMass => Body != null ? Body.mass : 1f;

    // True = carried as a dangling multi-body (ragdoll hung from a kinematic anchor via a
    // joint) instead of the kinematic transform-lock; see HeldItemController.Grab.
    public virtual bool DanglingCarry => false;

    // Dangling-carry bite stiffness: <= 0 = rigid grip (FixedJoint); > 0 = the body's
    // rotation is spring-driven toward the grip pose (ConfigurableJoint slerp drive),
    // so it sags and swings in the mouth. Damper kills the wobble.
    public virtual float DanglingRotationSpring => 0f;
    public virtual float DanglingRotationDamper => 0f;

    protected virtual void Awake() {
        conveyorObjectMover = GetComponentInParent<ConveyorObjectMover>();
        Body = GetComponent<Rigidbody>();
    }

    public virtual void OnHoldStart(RaycastHit hit, IsometricRaycaster arm) {
        Debug.Log($"Picked up item {hit.transform.name}");
        pickupActive = true;

        holder = HeldItemController.GetOrAdd(arm);
        holder.Grab(this);
        ItemEvents.ReportPickedUp(this);
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) { }

    public virtual void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
        pickupActive = false;

        if (holder != null) holder.Release();
        holder = null;
        ItemEvents.ReportDropped(this);
    }

    // Invoked by HeldItemController on EVERY release, right after the fling velocity is
    // applied to Body — multi-body carriers launch the rest of their rigidbodies here.
    public virtual void OnFlung(Vector3 flingVelocity) { }

    // Free-item impacts (ground, props, belt frames). Held-item hits are handled by
    // HeldItemHitForwarder on the same GameObject — skip those to avoid double-fire.
    protected virtual void OnCollisionEnter(Collision collision) {
        if (pickupActive) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed || Time.time < nextImpactTime) return;
        nextImpactTime = Time.time + minImpactInterval;

        ItemEvents.ReportItemImpact(this, collision, speed);
    }
}
