using System.Collections;
using UnityEngine;

public class LeverInteractable : MonoBehaviour, IInteractable {
    [Header("Lever Setup")]
    [SerializeField] private Transform leverPivot;
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float minAngle = -45f;
    [SerializeField] private float maxAngle = 45f;

    [Header("Input Tuning")]
    [SerializeField] private bool invertDrag = false;

    [Header("Screen-Space Drag")]
    [Tooltip("Primary mapping: map mouse Y between the lever tip's on-screen positions at min/max angle " +
             "(1:1 with the lever's real on-screen sweep). Turn off to use the fixed pixel band below.")]
    [SerializeField] private bool useTrueLeverExtent = true;
    [Tooltip("Fallback band height in pixels (used when 'useTrueLeverExtent' is off), centered on the " +
             "midpoint between the lever's min/max on-screen positions captured at grab time.")]
    [SerializeField] private float customRangePixels = 200f;

    [Header("Player Snap")]
    [Tooltip("World-units the duck is placed from the lever, along the swing-plane out-direction. " +
             "Independent of the cursor's engage range — the duck rolls here before the lever engages.")]
    [SerializeField] private float distanceFromLever = 1f;
    [Tooltip("Seconds for the duck to roll from where it grabbed to the aligned snap position.")]
    [SerializeField] private float rollDuration = 0.35f;

    [Header("Resistance")]
    [Tooltip("Base lever speed in degrees/second (at resistanceFactor = 1 and curve = 1).")]
    [SerializeField] private float maxLeverSpeed = 120f;
    [Tooltip("Higher = stiffer/slower lever (you must push longer). Scales follow speed by 1 / resistanceFactor.")]
    [SerializeField] private float resistanceFactor = 2f;
    [Tooltip("Follow-speed multiplier across the stroke. X = progress from minAngle (0) to maxAngle (1); " +
             "Y = speed multiplier (lower = stiffer at that point). Default is flat 1.")]
    [SerializeField] private AnimationCurve resistanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public InteractionType Type => InteractionType.Operate;

    private Quaternion startLocalRot;
    private float currentAngle;
    private bool isHeld;
    private bool isEngaged; // true once the duck has rolled in and the lever accepts drag

    // Per-grab cached state for tip-follow + screen-space mapping.
    private Transform rigTarget;
    private Camera cam;
    private Vector3 localGrabPoint;
    private float screenYAtMin;
    private float screenYAtMax;
    private float anchorCenterY;

    // Player refs (found lazily, like PickupInteractable).
    private PlayerDuckController playerDuckController;
    private CharacterController playerController;
    private Coroutine engageRoutine;

    private void Reset() {
        leverPivot = transform;
        localAxis = Vector3.right;
        minAngle = -45f;
        maxAngle = 45f;
        invertDrag = false;
        useTrueLeverExtent = true;
        customRangePixels = 200f;
        distanceFromLever = 1f;
        rollDuration = 0.35f;
        maxLeverSpeed = 120f;
        resistanceFactor = 2f;
        resistanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    }

    public void OnHoldStart(RaycastHit hit, Transform rigTarget) {
        if (leverPivot == null) leverPivot = transform;
        this.rigTarget = rigTarget;
        cam = Camera.main;

        if (playerDuckController == null) playerDuckController = FindFirstObjectByType<PlayerDuckController>();
        if (playerDuckController != null && playerController == null)
            playerController = playerDuckController.GetComponent<CharacterController>();

        startLocalRot = leverPivot.localRotation;
        currentAngle = 0f;
        isHeld = true;
        isEngaged = false;

        // Capture the grabbed point in the pivot's local space so it tracks the rotation.
        localGrabPoint = leverPivot.InverseTransformPoint(hit.point);

        // Precompute the tip's on-screen Y at both angle extremes for the screen-space mapping.
        if (cam != null) {
            screenYAtMin = cam.WorldToScreenPoint(GrabPointAtAngle(minAngle)).y;
            screenYAtMax = cam.WorldToScreenPoint(GrabPointAtAngle(maxAngle)).y;
            anchorCenterY = (screenYAtMin + screenYAtMax) * 0.5f;
        }

        ApplyAngle();

        // Open the bill a little so it reads as gripping the lever (closed again in OnHoldEnd).
        if (playerDuckController != null)
            playerDuckController.ToggleMouth(true, 0.2f);

        // Roll the duck into the lever's swing plane, then enable dragging.
        if (engageRoutine != null) StopCoroutine(engageRoutine);
        engageRoutine = StartCoroutine(EngageRoutine());
    }

    public void OnHoldDrag(RaycastHit hit, Vector2 mouseDelta) {
        if (!isHeld || !isEngaged) return;

        float targetAngle = MapMouseToAngle();

        // Rate-limited follow: the lever eases toward the mouse-implied angle but never snaps,
        // so quick flicks don't move it — you have to push deliberately. resistanceFactor sets
        // the overall stiffness; resistanceCurve varies it across the stroke.
        float progress = Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
        float curveMul = resistanceCurve != null ? Mathf.Max(0f, resistanceCurve.Evaluate(progress)) : 1f;
        float maxStep = (maxLeverSpeed / Mathf.Max(0.0001f, resistanceFactor)) * curveMul * Time.deltaTime;

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, maxStep);
        ApplyAngle();

        // Tip rides the grabbed point: not parented, just matched in world space each frame.
        if (rigTarget != null) rigTarget.position = leverPivot.TransformPoint(localGrabPoint);
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

        // Spring the lever back to neutral. (Remove these two lines if you want it to hold its angle.)
        currentAngle = 0f;
        ApplyAngle();
    }

    // Maps the current mouse Y to a target lever angle using the chosen screen-space scheme.
    private float MapMouseToAngle() {
        float mouseY = Input.mousePosition.y;

        float t;
        if (useTrueLeverExtent) {
            // Primary: mouse Y maps directly between the tip's min/max on-screen positions.
            t = Mathf.InverseLerp(screenYAtMin, screenYAtMax, mouseY);
        }
        else {
            // Fallback: a fixed pixel band centered on the lever's on-screen midpoint at grab time.
            float half = customRangePixels * 0.5f;
            t = Mathf.InverseLerp(anchorCenterY - half, anchorCenterY + half, mouseY);
        }

        if (invertDrag) t = 1f - t;
        return Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(t));
    }

    // Smoothly rolls the duck to the aligned snap position in the lever's swing plane,
    // then flips isEngaged so the lever starts accepting drag input.
    private IEnumerator EngageRoutine() {
        if (playerController != null) {
            Vector3 target = ComputeSnapPosition();
            Vector3 start = playerController.transform.position;
            float dur = Mathf.Max(0.0001f, rollDuration);

            for (float elapsed = 0f; elapsed < dur; elapsed += Time.deltaTime) {
                float k = Mathf.Clamp01(elapsed / dur);
                k = k * k * (3f - 2f * k); // smoothstep
                Vector3 desired = Vector3.Lerp(start, target, k);
                // Move via the CharacterController so the roll respects collisions.
                playerController.Move(desired - playerController.transform.position);
                yield return null;
            }
            playerController.Move(target - playerController.transform.position);
        }

        isEngaged = true;
        engageRoutine = null;
    }

    // Position directly out from the lever along its swing plane (horizontal direction perpendicular
    // to the rotation axis), on whichever side the duck is currently on, at distanceFromLever.
    // Keeps the duck's current Y — its resting ground height.
    private Vector3 ComputeSnapPosition() {
        Vector3 basePos = leverPivot.position;
        Vector3 duckPos = playerController.transform.position;

        Vector3 axisWorld = leverPivot.TransformDirection(
            (localAxis.sqrMagnitude > 0f) ? localAxis.normalized : Vector3.right);

        // Horizontal direction lying in the swing plane (perpendicular to the rotation axis).
        Vector3 outDir = Vector3.Cross(axisWorld, Vector3.up);
        outDir.y = 0f;

        // Degenerate case (rotation axis is vertical): fall back to the duck->lever approach direction.
        if (outDir.sqrMagnitude < 1e-6f) {
            outDir = duckPos - basePos;
            outDir.y = 0f;
        }
        outDir.Normalize();

        // Keep the duck on the side it is already on.
        Vector3 toDuck = duckPos - basePos; toDuck.y = 0f;
        if (Vector3.Dot(outDir, toDuck) < 0f) outDir = -outDir;

        return new Vector3(basePos.x, duckPos.y, basePos.z) + outDir * distanceFromLever;
    }

    private void ApplyAngle() {
        Vector3 axis = (localAxis.sqrMagnitude > 0f) ? localAxis.normalized : Vector3.right;
        leverPivot.localRotation = startLocalRot * Quaternion.AngleAxis(currentAngle, axis);
    }

    // World position of the grabbed point if the lever were rotated to 'angle', computed via a
    // local matrix so it does NOT disturb the live transform. Used at grab time to project the
    // tip's on-screen extent at the two angle limits.
    private Vector3 GrabPointAtAngle(float angle) {
        Vector3 axis = (localAxis.sqrMagnitude > 0f) ? localAxis.normalized : Vector3.right;
        Quaternion rot = startLocalRot * Quaternion.AngleAxis(angle, axis);
        Matrix4x4 pivotLocal = Matrix4x4.TRS(leverPivot.localPosition, rot, leverPivot.localScale);
        Matrix4x4 pivotWorld = (leverPivot.parent != null)
            ? leverPivot.parent.localToWorldMatrix * pivotLocal
            : pivotLocal;
        return pivotWorld.MultiplyPoint3x4(localGrabPoint);
    }

    // Optional helper if other systems want normalized lever value [0..1]
    public float GetNormalizedValue() {
        return Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
    }
}
