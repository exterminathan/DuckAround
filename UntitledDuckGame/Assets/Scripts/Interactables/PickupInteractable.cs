using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable {
    public InteractionType Type => InteractionType.Pickup;

    public void OnHoldStart(RaycastHit hit, Transform rigTarget) { }
    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) { }
    public void OnHoldEnd() { }
}
