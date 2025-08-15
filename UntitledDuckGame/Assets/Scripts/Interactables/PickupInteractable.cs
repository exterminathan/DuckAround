using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;

    public void OnHoldStart(RaycastHit hit, Transform rigTarget) {
        Debug.Log($"Picked up item {hit.transform.name}");
    }
    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        Debug.Log($"Dragging item {hit.transform.name} with mouse delta: {mouseDelta}");
    }
    public void OnHoldEnd() {
        Debug.Log("Pickup interaction ended.");
    }
}
