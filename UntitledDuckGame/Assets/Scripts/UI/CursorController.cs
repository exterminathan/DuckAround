using UnityEngine;

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

    [Header("Player Interaction")]
    [SerializeField] private PlayerDuckController playerDuckController;
    [SerializeField] private IsometricRaycaster isometricRaycaster;
    public float hoverEngageDistance = 2.75f;

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

        innerCursor.position = Vector3.SmoothDamp(
            innerCursor.position, mousePos, ref innerMoveVelocity, moveSmoothTime
        );

        bool hovering = false;
        var ray = Camera.main.ScreenPointToRay(mousePos);
        //check if hit interactive object, and is within distance to player
        if (Physics.Raycast(ray, out var hit) && hit.collider.CompareTag("Interactive"))
            hovering = true;

        Vector3 targetScale = hovering ? hoverScale : defaultScale;
        outerCursor.localScale = Vector3.SmoothDamp(
            outerCursor.localScale, targetScale, ref outerScaleVelocity, scaleSmoothTime
        );

        //get distance from player to hit location in 2d space

        Vector3 playerPosFlat = playerDuckController.transform.position;
        Vector3 hitPosFlat = hit.point;
        playerPosFlat.y = hitPosFlat.y = 0f;

        // Debug.Log($"Hit location: {hit.point}");
        // Debug.Log($"Player location: {playerDuckController.transform.position}");
        // Debug.Log($"Flat distance to player: {Vector3.Distance(playerPosFlat, hitPosFlat)}");

        float distanceToPlayer = Vector3.Distance(playerPosFlat, hitPosFlat);

        if (isometricRaycaster != null) {
            if (hovering && Input.GetMouseButtonDown(0) && distanceToPlayer < hoverEngageDistance) {
                isometricRaycaster.BeginHold(hit, playerDuckController);


            }
            if (isometricRaycaster.isHolding && Input.GetMouseButtonUp(0)) {
                isometricRaycaster.EndHold(playerDuckController);

            }
        }
    }
}