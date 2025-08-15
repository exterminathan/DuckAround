using UnityEngine;

public class LeverInteractable : MonoBehaviour, IInteractable {
    [Header("Lever Setup")]
    [SerializeField] private Transform leverPivot;
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float minAngle = -45f;
    [SerializeField] private float maxAngle = 45f;

    [Header("Input Tuning")]
    [SerializeField] private float degreesPerPixel = 0.15f;   // sensitivity
    [SerializeField] private bool invertDrag = false;         // invert if needed

    public InteractionType Type => InteractionType.Operate;

    private Quaternion startLocalRot;
    private float currentAngle;
    private bool isHeld;

    private void Reset() {
        leverPivot = transform;
        localAxis = Vector3.right;
        minAngle = -45f;
        maxAngle = 45f;
        degreesPerPixel = 0.15f;
        invertDrag = false;
    }

    public void OnHoldStart(RaycastHit hit, Transform rigTarget) {
        if (leverPivot == null) leverPivot = transform;
        startLocalRot = leverPivot.localRotation;
        currentAngle = 0f;
        isHeld = true;
        ApplyAngle();
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        if (!isHeld) return;

        float sign = invertDrag ? 1f : -1f;
        currentAngle += sign * mouseDelta.y * degreesPerPixel;
        currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
        ApplyAngle();
    }

    public void OnHoldEnd() {
        isHeld = false;
        // keep final angle; if you want it to spring back, uncomment next line:
        // currentAngle = 0f; ApplyAngle();
    }

    private void ApplyAngle() {
        Vector3 axis = (localAxis.sqrMagnitude > 0f) ? localAxis.normalized : Vector3.right;
        leverPivot.localRotation = startLocalRot * Quaternion.AngleAxis(currentAngle, axis);
    }

    // Optional helper if other systems want normalized lever value [0..1]
    public float GetNormalizedValue() {
        return Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
    }
}
