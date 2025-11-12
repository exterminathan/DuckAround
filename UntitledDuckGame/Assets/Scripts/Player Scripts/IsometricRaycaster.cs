// IsometricRaycaster.cs
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public enum InteractionType { None, Pickup, Operate }
public enum HoldMode { None, Pickup, Interact }

public interface IInteractable {
    InteractionType Type { get; }
    void OnHoldStart(RaycastHit hit, Transform rigTarget);
    void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta);
    void OnHoldEnd();
}

public class IsometricRaycaster : MonoBehaviour {
    #region Public Variables
    [Header("Setup")]
    public Camera mainCamera;
    public PlayerDuckController playerDuckController;

    [Header("IK Target")]
    [SerializeField] private Transform ik_target;
    public GameObject bone_point;
    public GameObject rotate_pivot;

    [Header("Rotation Parameters")]
    public float innerZoneRangeX = 225f;
    public float minPivotAngle = -90f;
    public float maxPivotAngle = 90f;
    public float rotationSmoothSpeed = 5f;
    private float rotationAngleY;

    [Header("Horizontal IK Parameters (Scroll Tip)")]
    public float minIKX = 0f;
    public float maxIKX = 0f;
    // how much scrolling moves the ik target 
    public float scrollIncrement = 0.1f;

    [Header("Vertical IK Parameters (Mouse Y Vertical)")]
    public float innerZoneRangeY = 200f;
    public float minIKY = 0.05f;
    public float maxIKY = 2.562f;
    public float ikVerticalSmoothSpeed = 5f;

    [Header("Boundary Visuals")]
    public Canvas uiCanvas;
    public Color boundaryColor = Color.white;
    public float fadeDistance = 50f;

    [Header("Debug Options")]
    public bool showDev = false;

    [Header("Collision Settings")]
    [Tooltip("Layers that block arm rotation")]
    public LayerMask rotationBlockingLayerMask;
    public LayerMask horizontalIKBlockingLayerMask;
    public LayerMask verticalIKBlockingLayerMask;
    public GameObject[] armObjects;
    public ArmHitForwarder[] armPushers { get; private set; }
    public bool isHolding { set; get; } = false;
    public bool isInteracting { set; get; } = false;
    #endregion

    #region Private Variables
    private Image leftBoundaryImage;
    private Image rightBoundaryImage;
    private Image topBoundaryImage;
    private Image bottomBoundaryImage;

    private Collider[] armColliders;

    [Header("Holding Settings")]
    private HoldMode _holdMode = HoldMode.None;
    private Vector3 preHoldRotation;
    private Vector3 preHoldIKPos;
    private Vector3 lastMousePos;
    private IInteractable activeInteractable;
    private RaycastHit holdHit;
    private Collider holdCollider;
    public Transform playerHoldSlot;
    #endregion

    #region Unity Functions
    void Start() {
        //asign cam and player ui
        if (mainCamera == null) mainCamera = Camera.main;
        if (uiCanvas == null) uiCanvas = FindFirstObjectByType<Canvas>();

        //assign default rotationAngleY
        rotationAngleY = rotate_pivot.transform.localEulerAngles.y;

        //populate arm colliders and pushers
        armColliders = armObjects
            .SelectMany(o => o.GetComponentsInChildren<Collider>())
            .Distinct()
            .ToArray();

        armPushers = armObjects
            .SelectMany(o => o.GetComponentsInChildren<ArmHitForwarder>())
            .Distinct()
            .ToArray();

        Debug.Log($"Armcolliders found: {(string.Join(", ", armColliders.Select(c => c.name)))}");
    }

    void Update() {

        if (_holdMode != HoldMode.Interact) {
            HandleRotation();
            HandleVerticalIK();
            HandleHorizontalIK();
        }
        else {
            HandleHoldInteraction();
        }
    }

    void LateUpdate() {
        //update arm pusher velocity/position
        foreach (var p in armPushers) {
            Vector3 currentPos = p.transform.position;
            Vector3 delta = currentPos - p.lastPos;

            p.velocity = delta / Time.deltaTime;
            p.lastPos = currentPos;
        }

    }
    #endregion

    #region Movement
    private void HandleRotation() {
        if (rotate_pivot == null) return;
        var pivot = rotate_pivot.transform;

        //HANDLE ROTATION SWEEP
        //REPLACE FROM HERE
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRangeX;
        float maxX = centerX + innerZoneRangeX;

        float t = Mathf.Clamp01((Input.mousePosition.x - minX) / (maxX - minX));
        float targetAngle = Mathf.Lerp(maxPivotAngle, minPivotAngle, t);

        float delta = Mathf.DeltaAngle(rotationAngleY, targetAngle);
        rotationAngleY += delta * Time.deltaTime * rotationSmoothSpeed;

        float currY = rotationAngleY;
        float rawDelta = delta;

        float allowedDelta = rawDelta;

        // sweep checks for arm colliders position after rotation about pivot
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

            Vector3 halfExtents = Vector3.Scale(box.size, box.transform.lossyScale);
            // box center after pivot rotation
            Vector3 worldOffset = box.transform.position - pivot.position;
            Vector3 rotatedCenter = pivot.position
                                  + Quaternion.Euler(0f, rawDelta, 0f)
                                    * worldOffset;
            // final orientation
            Quaternion rotatedOri = box.transform.rotation
                                  * Quaternion.Euler(0f, rawDelta, 0f);

            // clamp if overlapped
            Collider[] hits = Physics.OverlapBox(
                rotatedCenter,
                halfExtents,
                rotatedOri,
                rotationBlockingLayerMask,
                QueryTriggerInteraction.Ignore
            );
            if (hits.Length > 0) {
                // stop rotation entirely if any overlap found
                allowedDelta = 0f;
                if (showDev) Debug.Log($"[RotationBlocked] Arm collider {c.name} blocked by wall(s).");
                break;
            }

        }
        //TO HERE

        // finalized rotation
        var e = pivot.localEulerAngles;
        e.y = currY + allowedDelta;
        if (playerDuckController != null) pivot.localEulerAngles = e;
    }


    //Helper function for arm collider sweeps to ensure no collision with rotation blocking layer mask (walls, etc)
    private void HandleRotationSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

        }
    }

    //these two might need their own layer mask
    // so that it includes walls and also props, so tip/mouth cant clip thru those

    // Helper function for vertical IK sweep on IK Target to make 
    // sure its not going through rotation blocking layers/ objects
    private void HandleVerticalIKSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

        }
    }

    // Helper function for horizontal IK sweep on IK Target to make 
    // sure its not going through rotation blocking layers / objects
    private void HandleHorizontalIKSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

        }
    }



    private void HandleVerticalIK() {
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float t = Mathf.Clamp01((Input.mousePosition.y - minY) / (maxY - minY));

        float targetY = Mathf.Lerp(minIKY, maxIKY, t);
        Vector3 pos = ik_target.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * rotationSmoothSpeed * ikVerticalSmoothSpeed);
        ik_target.position = pos;
    }

    private void HandleHorizontalIK() {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f) {
            Vector3 local = ik_target.localPosition;
            float targetX = Mathf.Clamp(local.x - scroll * scrollIncrement, minIKX, maxIKX);
            local.x = Mathf.Lerp(local.x, targetX, 1f / 7f);
            ik_target.localPosition = local;
        }
    }

    public void RotateToTarget(Transform target) {
        if (target == null) return;

        preHoldRotation = rotate_pivot.transform.localEulerAngles;

        Vector3 dir = target.position - rotate_pivot.transform.position;
        dir.y = 0f;

        float targetY = Quaternion.LookRotation(dir).eulerAngles.y;
        Vector3 e = rotate_pivot.transform.localEulerAngles;
        e.y = targetY;
        rotate_pivot.transform.localEulerAngles = e;
    }
    public void ResetRotation() {
        rotate_pivot.transform.localEulerAngles = preHoldRotation;
    }
    #endregion

    #region Interaction
    private void HandleHoldInteraction() {
        Vector2 mouseDelta = (Vector2)(Input.mousePosition - lastMousePos);
        lastMousePos = Input.mousePosition;

        var ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (holdCollider != null && holdCollider.Raycast(ray, out var h, 1000f)) {
            holdHit = h;
        }

        if (activeInteractable != null) {
            activeInteractable.OnHoldDrag(holdHit, mouseDelta);
        }
    }

    public void BeginHold(RaycastHit hit, PlayerDuckController player) {
        holdHit = hit;
        lastMousePos = Input.mousePosition;
        preHoldRotation = rotate_pivot.transform.localEulerAngles;
        preHoldIKPos = ik_target.position;

        activeInteractable = hit.collider.GetComponent<IInteractable>();
        Debug.Log($"activeInteractable: {activeInteractable}");
        if (activeInteractable != null) {
            _holdMode = (activeInteractable.Type == InteractionType.Operate) ? HoldMode.Interact : HoldMode.Pickup;
            if (_holdMode == HoldMode.Interact) {
                isInteracting = true;
            }
            else {
                isInteracting = false;
            }
        }
        else {
            // Fallback by tag if no component present
            _holdMode = hit.collider.CompareTag("Interactive") ? HoldMode.Interact : HoldMode.Pickup;
        }

        string interactableTypeName = activeInteractable != null ? activeInteractable.GetType().Name : "nul";
        Debug.Log($"[InteractMode] {HoldMode.None} -> {_holdMode} using {interactableTypeName}");

        isHolding = true;
        holdCollider = hit.collider;

        if (_holdMode == HoldMode.Interact) {
            RotateToTarget(hit.transform);
            ik_target.position = hit.point;
            activeInteractable?.OnHoldStart(hit, ik_target);
        }
        if (_holdMode == HoldMode.Pickup) {
            activeInteractable?.OnHoldStart(hit, ik_target);

        }
    }

    public void EndHold(PlayerDuckController player) {
        if (!isHolding) return;

        if (_holdMode == HoldMode.Interact) {
            activeInteractable?.OnHoldEnd();
            ResetRotation();
            ik_target.position = preHoldIKPos;
        }
        if (_holdMode == HoldMode.Pickup) {
            activeInteractable?.OnHoldEnd();
        }


        if (_holdMode != HoldMode.None) {
            string interactableTypeName = activeInteractable != null ? activeInteractable.GetType().Name : "nul";
            Debug.Log($"[InteractMode] {_holdMode} -> {HoldMode.None} from {interactableTypeName}");
        }

        holdCollider = null;

        activeInteractable = null;
        _holdMode = HoldMode.None;
        isHolding = false;
        isInteracting = false;
    }
    #endregion

    #region Debug
    void CreateBoundaryLines() {
        System.Func<string, Image> makeLine = name => {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(uiCanvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, 0f);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        };
        leftBoundaryImage = makeLine("LeftInnerZoneLine");
        rightBoundaryImage = makeLine("RightInnerZoneLine");
        topBoundaryImage = makeLine("TopInnerZoneLine");
        bottomBoundaryImage = makeLine("BottomInnerZoneLine");
    }

    void UpdateBoundaryFade() {
        if (leftBoundaryImage == null) return;
        float cX = Screen.width * 0.5f, cY = Screen.height * 0.5f;
        float minX = cX - innerZoneRangeX, maxX = cX + innerZoneRangeX;
        float minY = cY - innerZoneRangeY, maxY = cY + innerZoneRangeY;
        float mX = Input.mousePosition.x, mY = Input.mousePosition.y;

        var leftRT = leftBoundaryImage.rectTransform;
        var rightRT = rightBoundaryImage.rectTransform;
        var topRT = topBoundaryImage.rectTransform;
        var bottomRT = bottomBoundaryImage.rectTransform;

        leftRT.anchoredPosition = new Vector2(minX - cX, 0f);
        rightRT.anchoredPosition = new Vector2(maxX - cX, 0f);
        topRT.anchoredPosition = new Vector2(0f, maxY - cY);
        bottomRT.anchoredPosition = new Vector2(0f, minY - cY);

        leftRT.sizeDelta = new Vector2(2f, Screen.height);
        rightRT.sizeDelta = new Vector2(2f, Screen.height);
        topRT.sizeDelta = new Vector2(Screen.width, 2f);
        bottomRT.sizeDelta = new Vector2(Screen.width, 2f);

        float alphaL = (mX <= minX + fadeDistance) ? Mathf.Clamp01(1f - ((mX - minX) / fadeDistance)) : 0f;
        float alphaR = (mX >= maxX - fadeDistance) ? Mathf.Clamp01(1f - ((maxX - mX) / fadeDistance)) : 0f;
        float alphaT = (mY >= maxY - fadeDistance) ? Mathf.Clamp01(1f - ((maxY - mY) / fadeDistance)) : 0f;
        float alphaB = (mY <= minY + fadeDistance) ? Mathf.Clamp01(1f - ((mY - minY) / fadeDistance)) : 0f;

        leftBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaL);
        rightBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaR);
        topBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaT);
        bottomBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaB);
    }

    void ShowDebugSphere(Vector3 position, Color debugColor) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, 1f);
    }
    #endregion
}
