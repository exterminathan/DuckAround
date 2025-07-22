using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour {
    [Header("Cursor Graphics")]
    public RectTransform innerCursor;
    public RectTransform outerCursor;

    [Header("Outer Ring Settings")]
    public Vector3 defaultScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.5f;
    public float scaleSmoothTime = 0.05f;

    [Header("Movement Settings")]
    public float moveSmoothTime = 0.02f;

    // ** Separate velocities for inner & outer **
    Vector3 innerMoveVelocity;
    Vector3 outerMoveVelocity;
    Vector3 outerScaleVelocity;

    void Awake() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    void Update() {
        Vector3 mousePos = Input.mousePosition;

        // 1) Both now use their own velocity
        innerCursor.position = Vector3.SmoothDamp(
            innerCursor.position, mousePos, ref innerMoveVelocity, moveSmoothTime
        );

        // 2) Raycast hover check (same as before)
        bool hovering = false;
        var ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out var hit) && hit.collider.CompareTag("Interactive"))
            hovering = true;

        // 3) Scale ring smoothly
        Vector3 targetScale = hovering ? hoverScale : defaultScale;
        outerCursor.localScale = Vector3.SmoothDamp(
            outerCursor.localScale, targetScale, ref outerScaleVelocity, scaleSmoothTime
        );
    }
}