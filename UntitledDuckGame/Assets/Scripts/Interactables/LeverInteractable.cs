using System.Collections;
using UnityEngine;

public class LeverInteractable : MonoBehaviour, IInteractable {
    [Header("Lever Setup")]
    [SerializeField] private Transform leverPivot;
    [Tooltip("Hinge axis in the PIVOT'S LOCAL space. The duck squares up in the plane perpendicular " +
             "to this, which is what makes the snap work for a lever mounted at any angle.")]
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float minAngle = -45f;
    [SerializeField] private float maxAngle = 45f;

    [Header("Drag Mapping")]
    [Tooltip("Push the cursor UP to drive the lever instead of down.")]
    [SerializeField] private bool invertDrag = false;
    [Tooltip("Cursor travel for a full stroke, as a fraction of screen height, measured down from " +
             "wherever the cursor was when you grabbed. 0.4 = drag down through 40% of the screen " +
             "to throw the lever end to end. Resolution-independent.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float travelScreenFraction = 0.4f;

    [Header("Player Snap")]
    [Tooltip("World units between the duck's arm pivot and the lever pivot once snapped in. " +
             "Independent of the cursor's engage range.")]
    [SerializeField] private float distanceFromLever = 1f;
    [Tooltip("How fast the duck rolls into position, in world units/second.")]
    [SerializeField] private float rollSpeed = 9f;
    [Tooltip("Hard cap on the roll in seconds, so a long approach can't stall the interaction.")]
    [SerializeField] private float maxRollDuration = 0.5f;
    [Tooltip("If the duck is already this close to the stand spot, skip the roll entirely and " +
             "attach on the spot.")]
    [SerializeField] private float snapTolerance = 0.2f;
    [Tooltip("Seconds to ease the hand from wherever it was onto the grabbed point, so the arm " +
             "reaches out instead of popping.")]
    [SerializeField] private float handReachTime = 0.15f;

    [Header("Tightness")]
    [Tooltip("Top follow speed in degrees/second, before tightness and the curve scale it down.")]
    [SerializeField] private float maxLeverSpeed = 120f;
    [Tooltip("How tightly the lever tracks the cursor. 1 = keeps up at full speed; low = heavy and " +
             "slow, so you have to keep pushing instead of flicking it.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float tightness = 0.5f;
    [Tooltip("Tightness across the stroke. X = where the lever currently sits, 0 at minAngle to 1 " +
             "at maxAngle. Y = multiplier on the follow speed at that point: 1 = full speed, " +
             "0 = seized solid. Use it for an easy start, a stiff spot mid-throw, or a hard grind " +
             "near the end.")]
    [SerializeField] private AnimationCurve tightnessCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Release")]
    [Tooltip("Spring the lever back to its authored rest pose when let go.")]
    [SerializeField] private bool returnToRestOnRelease = true;
    [Tooltip("Spring-back speed in degrees/second. 0 snaps instantly.")]
    [SerializeField] private float returnSpeed = 240f;

    public InteractionType Type => InteractionType.Operate;

    // Authored rest pose, captured ONCE. Capturing it per-grab let an interrupted spring-back
    // rebake the current (already rotated) pose as the new rest, so the lever crept every grab.
    private Quaternion restLocalRot;
    private bool restCaptured;

    private float currentAngle;
    private bool isHeld;
    private bool isEngaged; // true once the duck is in position and the lever accepts drag
    private bool springingBack;

    // Per-grab cached state.
    private IsometricRaycaster arm;
    private Vector3 localGrabPoint;
    private Vector3 standPos;
    private Vector3 approachDir;
    private Vector3 handStartPos;
    private float handBlend;

    // Drag anchor, captured the moment the lever engages: the cursor position and the lever
    // progress it corresponds to. Everything is measured RELATIVE to this, so engaging never
    // jerks the lever to wherever the cursor happened to be sitting.
    private float anchorMouseY;
    private float anchorProgress;

    // Player refs (found lazily, like PickupInteractable).
    private PlayerDuckController playerDuckController;
    private CharacterController playerController;
    private Coroutine engageRoutine;

    private float RestAngle => Mathf.Clamp(0f, Mathf.Min(minAngle, maxAngle), Mathf.Max(minAngle, maxAngle));

    private void Reset() {
        leverPivot = transform;
        localAxis = Vector3.right;
        minAngle = -45f;
        maxAngle = 45f;
        invertDrag = false;
        travelScreenFraction = 0.4f;
        distanceFromLever = 1f;
        rollSpeed = 9f;
        maxRollDuration = 0.5f;
        snapTolerance = 0.2f;
        handReachTime = 0.15f;
        maxLeverSpeed = 120f;
        tightness = 0.5f;
        tightnessCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        returnToRestOnRelease = true;
        returnSpeed = 240f;
    }

    private void Awake() {
        CaptureRest();
    }

    private void CaptureRest() {
        if (restCaptured) return;
        if (leverPivot == null) leverPivot = transform;
        restLocalRot = leverPivot.localRotation;
        currentAngle = RestAngle;
        restCaptured = true;
    }

    private void Update() {
        if (isHeld || !springingBack) return;

        float step = (returnSpeed <= 0f) ? Mathf.Infinity : returnSpeed * Time.deltaTime;
        currentAngle = Mathf.MoveTowards(currentAngle, RestAngle, step);
        ApplyAngle();
        if (Mathf.Abs(currentAngle - RestAngle) < 0.01f) {
            currentAngle = RestAngle;
            ApplyAngle();
            springingBack = false;
        }
    }

    public void OnHoldStart(RaycastHit hit, IsometricRaycaster arm) {
        CaptureRest();
        this.arm = arm;

        if (playerDuckController == null) playerDuckController = FindFirstObjectByType<PlayerDuckController>();
        if (playerDuckController != null && playerController == null)
            playerController = playerDuckController.GetComponent<CharacterController>();

        isHeld = true;
        isEngaged = false;
        springingBack = false;

        // Grabbed point in the pivot's local space, so it tracks the lever as it swings.
        localGrabPoint = leverPivot.InverseTransformPoint(hit.point);

        // Open the bill a little so it reads as gripping the lever (closed again in OnHoldEnd).
        if (playerDuckController != null)
            playerDuckController.ToggleMouth(true, 0.2f);

        // Where the duck has to stand for the ARM to line up with the lever. Derived from the
        // hinge axis, so it holds for a lever mounted at any angle. The body yaw is never touched
        // - the camera is parented to the body, so turning it would swing the whole view.
        float rollDist = 0f;
        if (playerDuckController != null && arm != null) {
            Transform body = playerDuckController.transform;
            Vector3 outDir = ComputeSwingOutDir(body.position - leverPivot.position);

            // Prefer the side the duck is already on, but if the arm can't swing that far round
            // (pivot clamp), take the other side of the swing plane instead.
            if (!arm.CanArmAim(-outDir) && arm.CanArmAim(outDir)) outDir = -outDir;

            approachDir = -outDir;
            if (!arm.ComputeArmAlignedStand(leverPivot.position, approachDir, distanceFromLever,
                                            out standPos)) {
                standPos = body.position;
            }

            Vector3 flatDelta = standPos - body.position;
            flatDelta.y = 0f;
            rollDist = flatDelta.magnitude;
        }

        if (engageRoutine != null) StopCoroutine(engageRoutine);

        // Already parked in front of the lever: attach on the spot, no roll, no wait.
        if (rollDist <= snapTolerance) {
            Engage();
        }
        else {
            engageRoutine = StartCoroutine(RollThenEngage(rollDist));
        }
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        if (!isHeld || !isEngaged) return;

        float targetAngle = MapCursorToAngle();

        // Rate-limited follow: the lever eases toward the cursor-implied angle but never snaps,
        // so quick flicks don't move it - you have to push deliberately. 'tightness' sets the
        // overall stiffness; 'tightnessCurve' varies it across the stroke.
        float progress = Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
        float curveMul = (tightnessCurve != null) ? Mathf.Max(0f, tightnessCurve.Evaluate(progress)) : 1f;
        float maxStep = maxLeverSpeed * Mathf.Max(0.001f, tightness) * curveMul * Time.deltaTime;

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, maxStep);
        ApplyAngle();

        // Hand rides the grabbed point: not parented, steered through the arm's API each frame.
        // Blended in over handReachTime so the arm reaches out rather than teleporting.
        if (arm != null) {
            Vector3 grabWorld = leverPivot.TransformPoint(localGrabPoint);
            handBlend = (handReachTime <= 0f)
                ? 1f
                : Mathf.MoveTowards(handBlend, 1f, Time.deltaTime / handReachTime);
            arm.SetArmTargetWorld(Vector3.Lerp(handStartPos, grabWorld, handBlend));
        }
    }

    public void OnHoldEnd() {
        isHeld = false;
        isEngaged = false;

        if (engageRoutine != null) {
            StopCoroutine(engageRoutine);
            engageRoutine = null;
        }

        // Close the bill again.
        if (playerDuckController != null)
            playerDuckController.ToggleMouth(false, 0.2f);

        if (returnToRestOnRelease) {
            if (returnSpeed <= 0f) {
                currentAngle = RestAngle;
                ApplyAngle();
            }
            else {
                springingBack = true; // Update() eases it home
            }
        }
    }

    // Rolls the duck to the aligned stand spot, then attaches. Duration scales with the distance
    // so a short shuffle doesn't cost the same as a long approach. The body yaw is untouched
    // throughout, so the isometric camera (a child of the body) never moves.
    private IEnumerator RollThenEngage(float rollDist) {
        Transform body = (playerDuckController != null) ? playerDuckController.transform : null;

        if (playerController != null && body != null) {
            float dur = Mathf.Min(rollDist / Mathf.Max(0.01f, rollSpeed), Mathf.Max(0.01f, maxRollDuration));
            Vector3 start = body.position;

            for (float elapsed = 0f; elapsed < dur; elapsed += Time.deltaTime) {
                float k = Mathf.Clamp01(elapsed / dur);
                k = k * k * (3f - 2f * k); // smoothstep
                Vector3 desired = Vector3.Lerp(start, standPos, k);
                // Move via the CharacterController so the roll respects collisions.
                playerController.Move(desired - body.position);
                yield return null;
            }
            playerController.Move(standPos - body.position);
        }

        Engage();
        engageRoutine = null;
    }

    // Latches on: aims the arm at the lever and opens the drag anchor. The arm swing and the hand
    // reach both ease from here on, concurrently with dragging - nothing is gated behind them, so
    // grabbing a lever you're already standing at is instant.
    private void Engage() {
        if (arm != null) {
            arm.SetInteractArmAim(approachDir);
            handStartPos = arm.ArmTargetWorld;
        }
        else {
            handStartPos = leverPivot.TransformPoint(localGrabPoint);
        }

        handBlend = 0f;
        anchorMouseY = Input.mousePosition.y;
        anchorProgress = Mathf.Clamp01(Mathf.InverseLerp(minAngle, maxAngle, currentAngle));
        isEngaged = true;
    }

    // Horizontal direction out of the lever, lying in its swing plane (perpendicular to the hinge),
    // on whichever side the duck already stands. 'fromPivotToDuck' is the duck's offset from the pivot.
    private Vector3 ComputeSwingOutDir(Vector3 fromPivotToDuck) {
        Vector3 axisWorld = leverPivot.TransformDirection(SafeAxis());

        Vector3 outDir = Vector3.Cross(axisWorld, Vector3.up);
        outDir.y = 0f;

        // Hinge is vertical (the lever sweeps horizontally): every horizontal direction lies in
        // the swing plane, so just approach from where the duck already is.
        if (outDir.sqrMagnitude < 1e-6f) {
            outDir = fromPivotToDuck;
            outDir.y = 0f;
            if (outDir.sqrMagnitude < 1e-6f) outDir = Vector3.forward;
        }
        outDir.Normalize();

        Vector3 toDuck = fromPivotToDuck;
        toDuck.y = 0f;
        if (Vector3.Dot(outDir, toDuck) < 0f) outDir = -outDir;

        return outDir;
    }

    // Cursor -> target angle, measured RELATIVE to where the cursor sat when the lever engaged.
    // At the anchor the lever holds its current angle; dragging away from the anchor walks it
    // toward whichever end you're heading for. That's what stops it snapping to an absolute
    // screen position the instant it latches on.
    private float MapCursorToAngle() {
        // Positive = dragged in the "pull" direction (down by default).
        float pull = invertDrag
            ? (Input.mousePosition.y - anchorMouseY)
            : (anchorMouseY - Input.mousePosition.y);

        // A full stroke is always this many pixels of travel from the anchor, whichever way you
        // drag - it no longer depends on where on the screen you happened to grab.
        float span = Mathf.Max(1f, Screen.height * travelScreenFraction);

        // Scaled by the stroke actually left in that direction, so a lever grabbed part-way
        // through still takes the full span to reach its end stop.
        float t = (pull >= 0f)
            ? anchorProgress + (1f - anchorProgress) * Mathf.Clamp01(pull / span)
            : anchorProgress - anchorProgress * Mathf.Clamp01(-pull / span);

        return Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(t));
    }

    private Vector3 SafeAxis() {
        return (localAxis.sqrMagnitude > 0f) ? localAxis.normalized : Vector3.right;
    }

    private void ApplyAngle() {
        leverPivot.localRotation = restLocalRot * Quaternion.AngleAxis(currentAngle, SafeAxis());
    }

    // Optional helper if other systems want normalized lever value [0..1]
    public float GetNormalizedValue() {
        return Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
    }
}
