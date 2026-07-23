using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;
    public bool pickupActive;

    [Header("Grip Tuning")]
    [Tooltip("Item size used for the mouth gape. 0 = measured from collider bounds.")]
    public float gripSize = 0f;
    [Tooltip("Local offset from the hold slot while carried.")]
    public Vector3 gripOffset = Vector3.zero;

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
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) { }

    public void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
        pickupActive = false;

        if (holder != null) holder.Release();
        holder = null;
    }
}
