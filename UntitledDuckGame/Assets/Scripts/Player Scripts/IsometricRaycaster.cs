// IsometricRaycaster.cs
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;


public enum InteractionType { None, Pickup, Operate }
public enum HoldMode { None, Pickup, Interact }

public interface IInteractable {
    InteractionType Type { get; }
    void OnHoldStart(RaycastHit hit, IsometricRaycaster arm);
    void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta);
    void OnHoldEnd();
}



public class IsometricRaycaster : MonoBehaviour {
    private enum FaceState { Idle, FacingTarget, Restoring }

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

    [Header("Interaction Facing")]
    [Tooltip("Degrees/second the duck body turns to face an Operate interactable, and turns back on release.")]
    public float bodyTurnSpeed = 360f;

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

    [Header("Upper Arm Box Sweep")]
    [Tooltip("Upper arm box width")]
    public float upperArmBoxWidth = 0.1f;
    [Tooltip("Upper arm box height")]
    public float upperArmBoxHeight = 0.1f;
    [Tooltip("Upper arm box rotation offset")]
    public Vector3 upperArmBoxRotationOffset = Vector3.zero;
    [Tooltip("Upper arm box center offset")]
    public Vector3 upperArmBoxCenterOffset = Vector3.zero;

    [Header("Bottom Arm Box Sweep")]
    [Tooltip("Bottom arm box width")]
    public float foreArmBoxWidth = 0.1f;
    [Tooltip("Bottom arm box height")]
    public float foreArmBoxHeight = 0.1f;
    [Tooltip("Bottom arm box rotation offset")]
    public Vector3 foreArmBoxRotationOffset = Vector3.zero;
    [Tooltip("Bottom arm box center offset.")]
    public Vector3 foreArmBoxCenterOffset = Vector3.zero;

    [Header("Arm Box Sweep - Shared")]
    public int sweepIterations = 8;
    public bool drawSweepDebug = true;

    [Header("Debug")]
    [Tooltip("Toggle key to lock ALL arm rotation + mouse/IK input")]
    public KeyCode debugLockKey = KeyCode.RightBracket;
    private bool debugInputLocked = false;

    // used to figure out per-arm velocity for collision impulse calculation
    public ArmHitForwarder[] armPushers { get; private set; }
    public bool isHolding { set; get; } = false;
    public bool isInteracting { set; get; } = false;
    #endregion

    #region Private Variables
    private BoxCollider[] armColliders;

    // Canonical local-space arm state (in ik_target's parent frame): reach from scroll,
    // fixed z captured at Start. Height comes from mouse Y each frame. The target's full
    // localPosition is recomposed from these every frame, so any perturbation (lever
    // tip-follow, collisions, restores) decays back onto the arm's axis instead of persisting.
    private Transform ikParent;
    private float reachX;
    private float defaultLocalZ;

    [Header("Holding Settings")]
    private HoldMode _holdMode = HoldMode.None;
    private Vector3 preHoldRotation;
    private float _neutralArmYaw;
    private float preHoldBodyYaw;
    private float bodyTargetYaw;
    private FaceState _faceState = FaceState.Idle;
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
        // neutral arm-pivot yaw = arm pointing straight ahead relative to the body
        _neutralArmYaw = rotate_pivot.transform.localEulerAngles.y;

        // seed the canonical local arm state from the authored rest pose
        ikParent = ik_target.parent;
        Vector3 initLocal = ik_target.localPosition;
        reachX = Mathf.Clamp(initLocal.x, minIKX, maxIKX);
        defaultLocalZ = initLocal.z;

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
        //
        // drawing the sweep boxes, so box width/height/offset/center can be tuned live during play.
        if (Input.GetKeyDown(debugLockKey)) {
            debugInputLocked = !debugInputLocked;
            Debug.Log($"[IsometricRaycaster] Debug input lock {(debugInputLocked ? "ON" : "OFF")}");
        }

        if (debugInputLocked) {
            DrawArmSweepDebug();
            return;
        }

        // Ease the duck body toward / back from an Operate target (keeps the arm in line
        // with the body instead of twisting it). No-op during normal play.
        UpdateBodyFacing();

        // if not interacting, handle rotation as normal
        if (_holdMode != HoldMode.Interact) {
            HandleRotation();
            HandleHorizontalIK();
            HandleVerticalIK();
        }
        else {
            HandleHoldInteraction();
        }
    }

    // While the debug input lock is on, redraw the arm sweep boxes at the current (frozen) IK pose
    // every frame so inspector tweaks (width/height/offset/center) refresh live without pausing.
    private void DrawArmSweepDebug() {
        if (tbikc == null || ik_target == null) return;
        ArmBoxesCollideAt(ik_target.position, true);
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

        //HANDLE ROTATION SWEEP
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRangeX;
        float maxX = centerX + innerZoneRangeX;

        float t = Mathf.Clamp01((Input.mousePosition.x - minX) / (maxX - minX));
        float targetAngle = Mathf.Lerp(maxPivotAngle, minPivotAngle, t);

        // Frame-rate-independent ease toward the target. Only this frame's step is swept,
        // and the accumulator advances only by what was actually applied, so rotationAngleY
        // always matches the pivot's real yaw (no snap when a blocked arm frees up).
        float k = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
        float step = Mathf.DeltaAngle(rotationAngleY, targetAngle) * k;

        // Sweep the arm colliders through this frame's yaw and clamp to the
        // furthest collision-free angle (partial clamp, not all-or-nothing).
        float allowedStep = HandleHorizontalIKSweep(step);

        if (playerDuckController != null) SetPivotYaw(rotationAngleY + allowedStep);
    }

    // Single write-path for the arm pivot's local yaw: keeps the rotationAngleY accumulator
    // in lockstep with the transform, so external writes (interact facing, restores) never
    // leave HandleRotation working from a stale angle.
    private void SetPivotYaw(float yaw) {
        var e = rotate_pivot.transform.localEulerAngles;
        e.y = yaw;
        rotate_pivot.transform.localEulerAngles = e;
        rotationAngleY = yaw;
    }
    public void RotateToTarget(Transform target) {
        if (target == null) return;

        preHoldRotation = rotate_pivot.transform.localEulerAngles;

        Vector3 dir = target.position - rotate_pivot.transform.position;
        dir.y = 0f;

        float targetY = Quaternion.LookRotation(dir).eulerAngles.y;
        SetPivotYaw(targetY);
    }
    public void ResetRotation() {
        rotate_pivot.transform.localEulerAngles = preHoldRotation;
        rotationAngleY = preHoldRotation.y;
    }

    // Set up a body-facing turn for an Operate interaction: park the arm pivot straight ahead
    // (so the arm stays in line with the body), remember the body's current facing for restore,
    // and seed the target yaw toward the interactable. The actual turn is eased in UpdateBodyFacing.
    private void BeginInteractFacing(RaycastHit hit) {
        if (playerDuckController == null) return;

        SetPivotYaw(_neutralArmYaw);

        // Only capture the rest yaw when fully idle: re-grabbing mid-restore would
        // otherwise capture a half-restored yaw and drift the body over repeated uses.
        if (_faceState == FaceState.Idle)
            preHoldBodyYaw = playerDuckController.transform.eulerAngles.y;

        Vector3 dir = hit.transform.position - playerDuckController.transform.position;
        dir.y = 0f;
        bodyTargetYaw = (dir.sqrMagnitude > 1e-6f)
            ? Quaternion.LookRotation(dir).eulerAngles.y
            : preHoldBodyYaw;

        _faceState = FaceState.FacingTarget;
    }

    // Eases the duck body to face the active Operate target, then back to its original facing on
    // release. While facing, it re-aims at the live target each frame (the duck may still be
    // rolling into position). Runs every frame but is a no-op when idle. This is the general guard
    // that keeps any Operate interactable from snapping the arm/bones off their yaw axis.
    private void UpdateBodyFacing() {
        if (_faceState == FaceState.Idle || playerDuckController == null) return;

        Transform body = playerDuckController.transform;

        if (_faceState == FaceState.FacingTarget && holdCollider != null) {
            Vector3 dir = holdCollider.transform.position - body.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f) bodyTargetYaw = Quaternion.LookRotation(dir).eulerAngles.y;
        }

        float goal = (_faceState == FaceState.FacingTarget) ? bodyTargetYaw : preHoldBodyYaw;
        float current = body.eulerAngles.y;
        float next = Mathf.MoveTowardsAngle(current, goal, bodyTurnSpeed * Time.deltaTime);

        Vector3 e = body.eulerAngles;
        e.y = next;
        body.eulerAngles = e;

        if (_faceState == FaceState.Restoring && Mathf.Abs(Mathf.DeltaAngle(next, goal)) < 0.05f) {
            _faceState = FaceState.Idle;
        }
    }
    #endregion

    #region Handle IK
    private void HandleVerticalIK() {
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float t = Mathf.Clamp01((Input.mousePosition.y - minY) / (maxY - minY));

        float targetY = Mathf.Lerp(minIKY, maxIKY, t);

        // Compose the FULL desired pose in the body-local frame (reach from scroll, height
        // from mouse Y, fixed z) so drift picked up during interactions decays back onto
        // the arm's axis instead of persisting. Height is body-relative, not world.
        Vector3 desiredLocal = new Vector3(reachX, targetY, defaultLocalZ);
        Vector3 desiredTargetPos = ikParent.TransformPoint(desiredLocal);

        // Limit the desired pose to the furthest point along the move that keeps
        // the arm meshes clear of verticalIKBlockingLayerMask geometry this frame.
        Vector3 safeTargetPos = HandleVerticalIKSweep(desiredTargetPos);

        // Frame-rate-independent smoothing toward the safe pose, applied in local space.
        Vector3 safeLocal = ikParent.InverseTransformPoint(safeTargetPos);
        float k = 1f - Mathf.Exp(-rotationSmoothSpeed * ikVerticalSmoothSpeed * Time.deltaTime);
        ik_target.localPosition = Vector3.Lerp(ik_target.localPosition, safeLocal, k);
    }

    private void HandleHorizontalIK() {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f) {
            reachX = Mathf.Clamp(reachX - scroll * scrollIncrement, minIKX, maxIKX);
        }
    }
    #endregion

    #region Handle Sweep
    // ---- Vertical IK sweep (mouse Y) ---------------------------------------
    // Prevents the arm meshes from clipping through verticalIKBlockingLayerMask
    // geometry when the IK target moves up/down. Returns the furthest position
    // along current->desired that keeps both arm segment boxes collision-free.
    // The arm pose for any candidate target is predicted with TwoBoneIKPreCalc.
    private Vector3 HandleVerticalIKSweep(Vector3 desiredTargetPos) {
        Vector3 currentTargetPos = ik_target.position;

        // Desired height is already clear -> allow the full move (draw final pose).
        if (!ArmBoxesCollideAt(desiredTargetPos, drawSweepDebug)) {
            return desiredTargetPos;
        }

        // Already overlapping at the current height -> don't push further in.
        if (ArmBoxesCollideAt(currentTargetPos, false)) {
            if (drawSweepDebug) ArmBoxesCollideAt(currentTargetPos, true);
            return currentTargetPos;
        }

        // Binary search the boundary between current (safe) and desired (blocked).
        float lo = 0f; // safe fraction
        float hi = 1f; // blocked fraction
        for (int i = 0; i < Mathf.Max(1, sweepIterations); i++) {
            float mid = (lo + hi) * 0.5f;
            Vector3 probe = Vector3.Lerp(currentTargetPos, desiredTargetPos, mid);
            if (ArmBoxesCollideAt(probe, false)) hi = mid;
            else lo = mid;
        }

        Vector3 safe = Vector3.Lerp(currentTargetPos, desiredTargetPos, lo);
        if (drawSweepDebug) ArmBoxesCollideAt(safe, true); // draw the chosen pose
        return safe;
    }

    // Predicts the arm pose for a target position and box-checks both segments.
    private bool ArmBoxesCollideAt(Vector3 targetPos, bool draw) {
        BoneTransforms b = TwoBoneIKPreCalc(tbikc.data, targetPos, draw);
        // Evaluate both so debug boxes draw for both; OR the results.
        bool upperHit = SegmentBoxCheck(b.rootPos, b.midPos, b.rootRot,
                                        upperArmBoxWidth, upperArmBoxHeight, upperArmBoxRotationOffset, upperArmBoxCenterOffset, draw);
        bool foreHit = SegmentBoxCheck(b.midPos, b.tipPos, b.midRot,
                                       foreArmBoxWidth, foreArmBoxHeight, foreArmBoxRotationOffset, foreArmBoxCenterOffset, draw);
        return upperHit || foreHit;
    }

    // Box-checks a single arm segment (a->b). The segment rotation's +Z already
    // runs along the bone (LookRotation from TwoBoneIKPreCalc); the per-arm rotationOffset
    // rolls/aligns the box to the real mesh. Width/Height are the box cross-section.
    private bool SegmentBoxCheck(Vector3 a, Vector3 b, Quaternion segmentRot, float width, float height, Vector3 rotationOffset, Vector3 centerOffset, bool draw) {
        float length = Vector3.Distance(a, b);
        Quaternion oriented = segmentRot * Quaternion.Euler(rotationOffset);
        // Start at the bone midpoint, then shift by centerOffset in the box's local axes.
        Vector3 center = (a + b) * 0.5f + oriented * centerOffset;
        Vector3 halfExtents = new Vector3(width * 0.5f, height * 0.5f, length * 0.5f);

        bool hit = Physics.CheckBox(center, halfExtents, oriented, verticalIKBlockingLayerMask, QueryTriggerInteraction.Ignore);

        if (draw) {
            ShowDebugBox(center, halfExtents * 2f, oriented, hit ? Color.red : Color.green);
        }
        return hit;
    }

    // ---- Horizontal / rotation sweep (mouse X) -----------------------------
    // Replaces the old all-or-nothing block in HandleRotation. Returns the
    // furthest yaw delta (about rotate_pivot) that keeps the arm colliders clear
    // of rotationBlockingLayerMask geometry this frame. TwoBoneIKPreCalc is not
    // used here: this is whole-bot rotation, not IK-target movement.
    private float HandleHorizontalIKSweep(float rawDelta) {
        if (Mathf.Abs(rawDelta) < 1e-4f) return rawDelta;

        // Full rotation is clear -> allow it.
        if (!RotationCollidesAt(rawDelta)) return rawDelta;

        // Binary search the boundary between 0 (safe) and rawDelta (blocked).
        float lo = 0f;       // safe delta
        float hi = rawDelta; // blocked delta
        for (int i = 0; i < Mathf.Max(1, sweepIterations); i++) {
            float mid = (lo + hi) * 0.5f;
            if (RotationCollidesAt(mid)) hi = mid;
            else lo = mid;
        }
        return lo;
    }

    // Rotates each arm collider about rotate_pivot by 'delta' (world yaw) and
    // tests overlap against rotationBlockingLayerMask.
    private bool RotationCollidesAt(float delta, bool draw = false) {
        if (armColliders == null || rotate_pivot == null) return false;
        Vector3 pivotPos = rotate_pivot.transform.position;
        Quaternion yaw = Quaternion.Euler(0f, delta, 0f);

        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

            // World-space center (respects box.center) and half extents.
            Vector3 worldCenter = box.transform.TransformPoint(box.center);
            Vector3 scale = box.transform.lossyScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(box.size.x * scale.x),
                Mathf.Abs(box.size.y * scale.y),
                Mathf.Abs(box.size.z * scale.z)) * 0.5f;

            Vector3 rotatedCenter = pivotPos + yaw * (worldCenter - pivotPos);
            Quaternion rotatedOri = yaw * box.transform.rotation;

            if (draw) ShowDebugBox(rotatedCenter, halfExtents * 2f, rotatedOri, Color.cyan);

            if (Physics.CheckBox(rotatedCenter, halfExtents, rotatedOri, rotationBlockingLayerMask, QueryTriggerInteraction.Ignore)) {
                return true;
            }
        }
        return false;
    }
    #endregion


    #region Helpers
    // calculates new positions of bones/colliders after vertical ik changes
    private BoneTransforms TwoBoneIKPreCalc(in TwoBoneIKConstraintData ikData, Vector3 calculatedTargetPos, bool debugDraw = false) {
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

        if (debugDraw) {
            Debug.DrawLine(rootPos, newMidPos, Color.yellow, 0.001f);
            Debug.DrawLine(newMidPos, newTipPos, Color.cyan, 0.001f);
            ShowDebugSphere((rootPos + newMidPos) / 2, Color.magenta);
            ShowDebugSphere((newTipPos + newMidPos) / 2, Color.magenta);
        }


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
    // Interactables steer the arm through this instead of touching the rig target
    // Transform directly, so the raycaster stays the single owner of arm state.
    public void SetArmTargetWorld(Vector3 worldPos) {
        ik_target.position = worldPos;
    }

    // Interface references bypass Unity's overloaded null check, so a destroyed
    // interactable would otherwise still receive calls. Clear it explicitly.
    private void ValidateActiveInteractable() {
        if (activeInteractable is Object o && o == null) activeInteractable = null;
    }

    private void HandleHoldInteraction() {
        ValidateActiveInteractable();

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

        // parent search: free items are often hit on a child collider (e.g. the mesh
        // collider on the model child) while the interactable lives on the root
        activeInteractable = hit.collider.GetComponentInParent<IInteractable>();
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
            BeginInteractFacing(hit);
            ik_target.position = hit.point;
            activeInteractable?.OnHoldStart(hit, this);
        }
        if (_holdMode == HoldMode.Pickup) {
            activeInteractable?.OnHoldStart(hit, this);

        }
    }

    public void EndHold(PlayerDuckController player) {
        if (!isHolding) return;
        ValidateActiveInteractable();

        if (_holdMode == HoldMode.Interact) {
            activeInteractable?.OnHoldEnd();
            ResetRotation();
            // No snap-restore of the IK target: the per-frame local composition in
            // HandleVerticalIK blends the arm back onto its axis over a few frames.
            // Ease the body back to its pre-interaction facing (UpdateBodyFacing handles it).
            _faceState = FaceState.Restoring;
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

    // Rotated wireframe box: corners are built in local space then rotated about 'center'.
    // Used to visualize the arm sweep boxes so their orientation can be tuned.
    public static void ShowDebugBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration = 0.001f) {
        Vector3 e = size * 0.5f;
        Vector3 p000 = center + rotation * new Vector3(-e.x, -e.y, -e.z);
        Vector3 p100 = center + rotation * new Vector3(+e.x, -e.y, -e.z);
        Vector3 p001 = center + rotation * new Vector3(-e.x, -e.y, +e.z);
        Vector3 p101 = center + rotation * new Vector3(+e.x, -e.y, +e.z);
        Vector3 p010 = center + rotation * new Vector3(-e.x, +e.y, -e.z);
        Vector3 p110 = center + rotation * new Vector3(+e.x, +e.y, -e.z);
        Vector3 p011 = center + rotation * new Vector3(-e.x, +e.y, +e.z);
        Vector3 p111 = center + rotation * new Vector3(+e.x, +e.y, +e.z);

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

    // Line-drawn marker. (The old version spawned a primitive GameObject and leaked a
    // material instance per call — too expensive at per-frame/per-collision call rates.)
    public static void ShowDebugSphere(Vector3 position, Color debugColor, float duration = 0.02f) {
        const float r = 0.06f;
        Debug.DrawLine(position + Vector3.left * r, position + Vector3.right * r, debugColor, duration);
        Debug.DrawLine(position + Vector3.down * r, position + Vector3.up * r, debugColor, duration);
        Debug.DrawLine(position + Vector3.back * r, position + Vector3.forward * r, debugColor, duration);
    }
    #endregion
}
