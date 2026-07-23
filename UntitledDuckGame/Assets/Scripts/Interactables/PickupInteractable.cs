using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;
    public bool pickupActive;

    [Header("Grip Tuning")]
    [Tooltip("Item size used for the mouth gape. 0 = measured from collider bounds.")]
    public float gripSize = 0f;
    [Tooltip("Local offset from the hold slot while carried.")]
    public Vector3 gripOffset = Vector3.zero;

    [Header("Impact Reporting")]
    [Tooltip("Minimum collision speed (m/s) that counts as an ItemEvents impact.")]
    public float minImpactSpeed = 0.5f;
    [Tooltip("Seconds between reported impacts, so rolling/sliding contact doesn't spam.")]
    public float minImpactInterval = 0.1f;
    private float nextImpactTime;

    private ConveyorObjectMover conveyorObjectMover;
    private HeldItemController holder;

    public ConveyorObjectMover ConveyorMover => conveyorObjectMover;
    public Rigidbody Body { get; private set; }

    private void Awake() {
        conveyorObjectMover = GetComponentInParent<ConveyorObjectMover>();
        Body = GetComponent<Rigidbody>();
    }

    public void OnHoldStart(RaycastHit hit, IsometricRaycaster arm) {
        Debug.Log($"Picked up item {hit.transform.name}");
        pickupActive = true;

        holder = HeldItemController.GetOrAdd(arm);
        holder.Grab(this);
        ItemEvents.ReportPickedUp(this);
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) { }

    public void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
        pickupActive = false;

        if (holder != null) holder.Release();
        holder = null;
        ItemEvents.ReportDropped(this);
    }

    // Free-item impacts (ground, props, belt frames). Held-item hits are handled by
    // HeldItemHitForwarder on the same GameObject — skip those to avoid double-fire.
    private void OnCollisionEnter(Collision collision) {
        if (pickupActive) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed || Time.time < nextImpactTime) return;
        nextImpactTime = Time.time + minImpactInterval;

        ItemEvents.ReportItemImpact(this, collision, speed);
    }
}
