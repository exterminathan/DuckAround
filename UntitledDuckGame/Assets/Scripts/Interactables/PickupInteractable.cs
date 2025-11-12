using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;
    public bool pickupActive;

    private IsometricRaycaster isometricRaycaster;
    private PlayerDuckController playerDuckController;
    private ConveyorObjectMover conveyorObjectMover;
    private Rigidbody rb;

    private void Start() {
        isometricRaycaster = FindFirstObjectByType<IsometricRaycaster>();
        playerDuckController = FindFirstObjectByType<PlayerDuckController>();
        conveyorObjectMover = GetComponentInParent<ConveyorObjectMover>();
        rb = GetComponent<Rigidbody>();

    }


    private void Update() {
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

    public void OnHoldStart(RaycastHit hit, Transform rigTarget) {
        Debug.Log($"Picked up item {hit.transform.name}");
        pickupActive = true;

        //open mouth
        StartCoroutine(playerDuckController.ToggleMouth(true, 0.25f));
        //rb kinematic

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        //set parent
        transform.SetParent(isometricRaycaster.playerHoldSlot);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        //release from conveyor if on one
        if (conveyorObjectMover != null) {
            conveyorObjectMover.ReleaseFromConveyor();
        }


    }
    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        Debug.Log($"Dragging item {hit.transform.name} with mouse delta: {mouseDelta}");
    }
    public void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
        pickupActive = false;

        //close mouth
        StartCoroutine(playerDuckController.ToggleMouth(false, 0.25f));

        //rb non kinematic


        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = true;
        rb.isKinematic = false;

        //clear parent
        transform.SetParent(null);


    }
}
