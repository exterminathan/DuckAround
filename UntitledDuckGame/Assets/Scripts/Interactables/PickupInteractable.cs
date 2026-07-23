using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;
    public bool pickupActive;

    private PlayerDuckController playerDuckController;
    private ConveyorObjectMover conveyorObjectMover;
    private Rigidbody rb;
    private RigidbodyInterpolation cachedInterpolation;

    private void Start() {
        playerDuckController = FindFirstObjectByType<PlayerDuckController>();
        conveyorObjectMover = GetComponentInParent<ConveyorObjectMover>();
        rb = GetComponent<Rigidbody>();

    }


    private void Update() {
        //while held, the hold owns the item — the conveyor must not reclaim it
        if (pickupActive) return;

        if (transform.hasChanged) {
            transform.hasChanged = false;

            //if y position below .875f
            if (transform.position.y < 0.875f) {
                if (conveyorObjectMover != null && !conveyorObjectMover.enabled) {
                    conveyorObjectMover.ReactivateFromWorldDrop();
                }
            }

        }

    }

    public void OnHoldStart(RaycastHit hit, IsometricRaycaster arm) {
        Debug.Log($"Picked up item {hit.transform.name}");
        pickupActive = true;

        //open mouth
        playerDuckController.ToggleMouth(true, 0.25f);

        //release from conveyor BEFORE taking over the rigidbody — it flips the rb
        //back to dynamic + gravity, which would undo the kinematic hold below
        if (conveyorObjectMover != null) {
            conveyorObjectMover.ReleaseFromConveyor();
        }

        //interpolation must be off while held: it re-stamps the stale physics-body
        //pose over parent-driven motion every frame, freezing the item in world space
        cachedInterpolation = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;

        //rb kinematic
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        //set parent
        transform.SetParent(arm.playerHoldSlot);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;


    }
    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        Debug.Log($"Dragging item {hit.transform.name} with mouse delta: {mouseDelta}");
    }
    public void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
        pickupActive = false;

        //close mouth
        playerDuckController.ToggleMouth(false, 0.25f);

        //rb non kinematic (before zeroing velocity — kinematic bodies reject velocity writes)
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.interpolation = cachedInterpolation;

        //clear parent
        transform.SetParent(null);


    }
}
