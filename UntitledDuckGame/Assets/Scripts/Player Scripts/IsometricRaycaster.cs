// IsometricRaycaster.cs
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
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
    private struct BoneTransforms {
        public Vector3 rootPos;
        public Quaternion rootRot;
        public Vector3 midPos;
        public Quaternion midRot;
        public Vector3 tipPos;
        public Quaternion tipRot;
    }

    #region Public Variables
    [Header("Setup")]
    public Camera mainCamera;
    public PlayerDuckController playerDuckController;

    [Header("IK Target")]
    [SerializeField] private Transform ik_target;
    public GameObject bone_point;
    public GameObject rotate_pivot;
    public TwoBoneIKConstraint tbikc;

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

    [Header("Collision Settings")]
    [Tooltip("Layers that block arm rotation")]
    public LayerMask rotationBlockingLayerMask;
    public LayerMask horizontalIKBlockingLayerMask;
    public LayerMask verticalIKBlockingLayerMask;
    public GameObject[] armObjects;

    // used to figure out per-arm velocity for collision impulse calculation
    public ArmHitForwarder[] armPushers { get; private set; }
    public bool isHolding { set; get; } = false;
    public bool isInteracting { set; get; } = false;
    #endregion

    #region Private Variables
    private BoxCollider[] armColliders;

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
            .SelectMany(o => o.GetComponentsInChildren<BoxCollider>())
            .Distinct()
            .ToArray();

        armPushers = armObjects
            .SelectMany(o => o.GetComponentsInChildren<ArmHitForwarder>())
            .Distinct()
            .ToArray();

        Debug.Log($"Armcolliders found: {(string.Join(", ", armColliders.Select(c => c.name)))}");


    }

    void Update() {

        // if not interacting, handle rotation as normal
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

            // figure out velocity for all ArmHitForwarders
            // for use in collision impulse calculation
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
                Debug.Log($"[RotationBlocked] Arm collider {c.name} blocked by wall(s).");
                break;
            }

        }
        //TO HERE

        // finalized rotation
        var e = pivot.localEulerAngles;
        e.y = currY + allowedDelta;
        if (playerDuckController != null) pivot.localEulerAngles = e;
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

    #region Handle IK
    private void HandleVerticalIK() {
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float t = Mathf.Clamp01((Input.mousePosition.y - minY) / (maxY - minY));

        float targetY = Mathf.Lerp(minIKY, maxIKY, t);

        Vector3 calculatedTargetPos = new Vector3(ik_target.position.x, targetY, ik_target.position.z);
        // check here before applying lerp to pos.y with handlerverticaliksweep
        // whether the new position would cause a collision
        HandleVerticalIKSweep(calculatedTargetPos);
        // if there is collision, adjust targetY accordingly
        // if no collision, can use targetY as is

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
    #endregion

    #region Handle Sweep
    //Helper function for arm collider sweeps to ensure no collision with rotation blocking layer mask (walls, etc)
    private void HandleRotationSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

        }
    }

    // if there are collisions, adjust ik target position to prevent clipping
    // necessary parameters: currentIKPos
    private void HandleVerticalIKSweep(Vector3 calculatedTargetPos) {
        BoneTransforms predictedValues = TwoBoneIKPreCalc(tbikc.data, calculatedTargetPos);

        //now that we have all this info
        //we can do a capsule cast check for the root-mid and mid-tip segments with some thickness
        //i get thru experimentation

        //get half way point between root and mid

        //cast capsule stretching from root to mid centered on midpoint
        // with radius of whatever we found

        //check for collisions with layer horizontalIKBlockingLayerMask

        //if none found, return

        //if found, find closest point along line from calculatedTargetPos to current tip pos that is on surface of object hit
        //and set calculatedTargetPos to that point minus some small offset in direction away from hit normal

        //ref unity vector3 math: https://chatgpt.com/c/69162ab2-cd58-832e-b3cd-12a381668390



    }

    // Helper function for horizontal IK sweep on IK Target to make 
    // sure its not going through rotation blocking layers / objects
    private void HandleHorizontalIKSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

        }
    }

    #endregion



    #region Helpers
    // calculates new positions of bones/colliders after vertical ik changes
    private BoneTransforms TwoBoneIKPreCalc(in TwoBoneIKConstraintData ikData, Vector3 calculatedTargetPos) {
        Transform root = ikData.root;
        Transform mid = ikData.mid;
        Transform tip = ikData.tip;

        Vector3 rootPos = root.position;
        Vector3 midPos = mid.position;
        Vector3 tipPos = tip.position;

        Vector3 tbikTargetPos = ikData.target.position;
        Vector3 hintPos = ikData.hint.position;

        // if ((tbikTargetPos - calculatedTargetPos).magnitude > 0.001f) {
        //     Debug.Log("\nCurrent target pos thru two bone ik:" + tbikTargetPos);
        //     Debug.Log("Current ik target position from calc:" + calculatedTargetPos);
        // }

        float lenA = Vector3.Distance(rootPos, midPos);
        float lenB = Vector3.Distance(midPos, tipPos);

        // solve 2 bone IK same way unity does with law of cosines

        //clamp target if unreachable
        float totalLen = lenA + lenB;

        Vector3 dir = calculatedTargetPos - rootPos;
        float dist = dir.magnitude;

        if (dist > totalLen) {
            dir = dir.normalized * totalLen;
            calculatedTargetPos = rootPos + dir;
            dist = totalLen;
        }

        // law of cosines to solve elbow angle
        //angle at root joint
        float angleA = Mathf.Acos(Mathf.Clamp(
            (lenA * lenA + dist * dist - lenB * lenB) / (2f * lenA * dist), -1, 1
        ));

        //angle at mid joint
        float angleB = Mathf.Acos(Mathf.Clamp(
            (lenA * lenA + lenB * lenB - dist * dist) / (2f * lenA * lenB), -1, 1
        ));

        //compute new mid position
        //project from root to target
        Vector3 rootToTargetDir = (calculatedTargetPos - rootPos).normalized;
        Vector3 midStraight = rootPos + rootToTargetDir * lenA;

        // bend using hint vector
        Vector3 bendNormal;
        if (hintPos != null) {
            bendNormal = Vector3.Cross(rootToTargetDir, (rootPos - hintPos)).normalized;
        }
        else {
            bendNormal = Vector3.Cross(rootToTargetDir, Vector3.up).normalized;
        }

        //rotate straight line outwards by angleA around that plane
        Quaternion bendRotation = Quaternion.AngleAxis(-Mathf.Rad2Deg * angleA, bendNormal);
        Vector3 newMidPos = rootPos + (bendRotation * rootToTargetDir) * lenA;


        Vector3 newTipPos = newMidPos + (calculatedTargetPos - newMidPos).normalized * lenB;

        //compute world rotation for both bones
        Quaternion newRootRot = Quaternion.LookRotation(newMidPos - rootPos, bendNormal);
        Quaternion newMidRot = Quaternion.LookRotation(newTipPos - newMidPos, bendNormal);

        Debug.DrawLine(rootPos, newMidPos, Color.yellow, 0.001f);
        Debug.DrawLine(newMidPos, newTipPos, Color.cyan, 0.001f);
        ShowDebugSphere((rootPos + newMidPos) / 2, Color.magenta);
        ShowDebugSphere((newTipPos + newMidPos) / 2, Color.magenta);


        // Debug.Log(
        //     $"newRootPos: {rootPos} " +
        //     $"newMidPos: {newMidPos} " +
        //     $"newTipPos: {newTipPos} " +
        //     $"newRootRot: {newRootRot.eulerAngles} " +
        //     $"newMidRot: {newMidRot.eulerAngles} "
        // );

        return new BoneTransforms {
            rootPos = rootPos,
            rootRot = newRootRot,
            midPos = newMidPos,
            midRot = newMidRot,
            tipPos = newTipPos,
            tipRot = Quaternion.LookRotation(newTipPos - newMidPos, bendNormal)
        };
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
    public static void ShowDebugBox(Vector3 center, Vector3 size, Color color, float duration = 0.001f) {
        Vector3 e = size * 0.5f;
        Vector3 p000 = center + new Vector3(-e.x, -e.y, -e.z);
        Vector3 p100 = center + new Vector3(+e.x, -e.y, -e.z);
        Vector3 p001 = center + new Vector3(-e.x, -e.y, +e.z);
        Vector3 p101 = center + new Vector3(+e.x, -e.y, +e.z);
        Vector3 p010 = center + new Vector3(-e.x, +e.y, -e.z);
        Vector3 p110 = center + new Vector3(+e.x, +e.y, -e.z);
        Vector3 p011 = center + new Vector3(-e.x, +e.y, +e.z);
        Vector3 p111 = center + new Vector3(+e.x, +e.y, +e.z);

        // bottom face
        Debug.DrawLine(p000, p100, color, duration);
        Debug.DrawLine(p100, p101, color, duration);
        Debug.DrawLine(p101, p001, color, duration);
        Debug.DrawLine(p001, p000, color, duration);
        // top face
        Debug.DrawLine(p010, p110, color, duration);
        Debug.DrawLine(p110, p111, color, duration);
        Debug.DrawLine(p111, p011, color, duration);
        Debug.DrawLine(p011, p010, color, duration);
        // verticals
        Debug.DrawLine(p000, p010, color, duration);
        Debug.DrawLine(p100, p110, color, duration);
        Debug.DrawLine(p101, p111, color, duration);
        Debug.DrawLine(p001, p011, color, duration);
    }

    public static void ShowDebugSphere(Vector3 position, Color debugColor) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<SphereCollider>());
        sphere.layer = LayerMask.NameToLayer("Debug");
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, .01f);
    }
    #endregion
}
