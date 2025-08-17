// CursorController.cs
using Mono.Cecil.Cil;
using UnityEngine;

public class CursorController : MonoBehaviour {
    [Header("Cursor Graphics")]
    public RectTransform innerCursor;
    public RectTransform outerCursor;

    [Header("Cursor Colors")]
    public Color idleColor;
    public Color hoverColor;
    public Color holdColor;

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

    private bool isHovering = false;
    private bool inRange = false;

    //public reference to where cursor hit
    [HideInInspector] public Ray cursorHit;

    void Awake() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        //set inner and outter cursors to idlecolor
        SetCursorColor(idleColor, 0);
    }

    void Update() {
        Vector3 mousePos = Input.mousePosition;

        // damp cursor movement
        innerCursor.position = Vector3.SmoothDamp(
            innerCursor.position, mousePos, ref innerMoveVelocity, moveSmoothTime
        );

        //check if hit interactive object, and is within distance to player
        var ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out var hit) && hit.collider.CompareTag("Interactive")) {
            isHovering = true;

            //get distance from player to hit location in 2d space
            Vector3 playerPosFlat = playerDuckController.transform.position;
            Vector3 hitPosFlat = hit.point;
            playerPosFlat.y = hitPosFlat.y = 0f;

            float distanceToPlayer = Vector3.Distance(playerPosFlat, hitPosFlat);
            inRange = distanceToPlayer < hoverEngageDistance;
        }
        else {
            isHovering = false;
            inRange = false;
        }

        //scale outer cursor based on hover state
        Vector3 targetScale = isHovering ? hoverScale : defaultScale;
        outerCursor.localScale = Vector3.SmoothDamp(outerCursor.localScale, targetScale, ref outerScaleVelocity, scaleSmoothTime);
        SetCursorColor(isHovering ? hoverColor : idleColor, 2);


        if (isometricRaycaster != null) {
            if (isHovering && Input.GetMouseButtonDown(0) && inRange) {
                isometricRaycaster.BeginHold(hit, playerDuckController);
                SetCursorColor(holdColor, 1);


            }
            if (isometricRaycaster.isHolding && Input.GetMouseButtonUp(0)) {
                isometricRaycaster.EndHold(playerDuckController);
                SetCursorColor(idleColor, 1);

            }
        }

    }

    /// <summary>
    /// Sets the cursor color based on the provided flag.
    /// 1 for inner cursor, 2 for outer cursor, 0 for both.
    /// </summary>
    public void SetCursorColor(Color color, float cursorFlag) {
        switch (cursorFlag) {
            case 1: //set only inner cursor to color
                innerCursor.GetComponent<UnityEngine.UI.Image>().color = color;
                break;
            case 2: //set only outer cursor to color
                outerCursor.GetComponent<UnityEngine.UI.Image>().color = color;
                break;
            default:
                innerCursor.GetComponent<UnityEngine.UI.Image>().color = color;
                outerCursor.GetComponent<UnityEngine.UI.Image>().color = color;
                break;
        }
    }
}