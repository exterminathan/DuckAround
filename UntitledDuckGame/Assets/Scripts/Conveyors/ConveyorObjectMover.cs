using System;
using UnityEngine;

// Belt riding is PHYSICAL: on-belt items stay dynamic (gravity off) and a velocity
// servo steers them along the path each physics step. Collisions are real — items
// shove each other, press against blockers, and get pushed around by the arm/player.
// The track position re-syncs to wherever physics actually let the item go; if it's
// forced too far off the line it releases back to a free prop.
public class ConveyorObjectMover : MonoBehaviour
{
    public enum MoverState { OnBelt, Free }

    [Header("Object Mover Settings")]
    [Tooltip("The belt currently being ridden. Auto-assigned when a belt captures this item — no pre-wiring needed. Speed/loop/exit force live on the ConveyorPath.")]
    public ConveyorPath path;

    [Tooltip("Smoothing window for path tangents (corner rounding).")]
    public float tangentHalfWindow = 0.25f;
    [Tooltip("How fast the item reorients to the belt direction (deg/sec).")]
    public float maxTurnRateDegPerSec = 720f;

    [Header("Re-Snap Settings")]
    [SerializeField] private float snapDistance = 0.1f;
    [Tooltip("Item must be at/below this speed to be captured back onto the belt.")]
    [SerializeField] private float restSpeed = 0.35f;
    [Tooltip("Seconds after a fling/knock-off before the belt may recapture the item.")]
    [SerializeField] private float resnapCooldown = 0.5f;
    [SerializeField] private float resnapCheckInterval = 0.2f;
    [Tooltip("Max height above/below the path spine at which an item can still be captured.")]
    [SerializeField] private float snapHeightTolerance = 0.5f;
    [Tooltip("Item must also be tumbling at/below this angular speed (rad/s) to be captured.")]
    [SerializeField] private float restAngularSpeed = 1f;

    [Header("Ride Settle")]
    [Tooltip("How fast a tilted item flattens onto its nearest face while riding (deg/sec). High = near-instant.")]
    [SerializeField] private float tiltSettleDegPerSec = 240f;
    [Tooltip("How fast the drop height eases onto the belt surface (units/sec).")]
    [SerializeField] private float rideHeightSettleSpeed = 4f;

    // Placement tolerance for the initial on-belt check at Start: hand-placed scene
    // items rarely sit exactly on the spine, so this is laxer than snapDistance.
    private const float placementCaptureDistance = 0.5f;

    // how much faster than belt speed the servo may move to recover toward the track
    private const float maxCorrectionSpeed = 2f;
    // shoved further than this off the ride line (horizontally) -> knocked off
    private const float releaseDeviationPad = 0.25f;
    // free items whose pivot is within this margin beyond the belt edge still capture
    private const float captureEdgeMargin = 0.15f;
    // cursor stall control: full speed until the item lags its lane point by more
    // than the slack, tapering to a stop across the span (blocked items press gently)
    private const float lagSlack = 0.05f;
    private const float lagSpan = 0.3f;
    // soft-servo gains (per second): proportional pull toward the target pose —
    // gentle on purpose, so persistent contacts damp instead of stick-slipping
    private const float posCorrectionGain = 6f;
    private const float angCorrectionGain = 6f;
    // ride slightly above the contact plane so riders aren't permanently touching it
    private const float rideHoverClearance = 0.02f;

    private float s;

    // Captured ride pose relative to the belt tangent frame, taken at every capture
    // (scene start or resnap): an item rides in whatever orientation and lateral
    // offset it was placed/dropped with. Height eases toward rideRestHeight — the
    // pivot-to-collider-bottom distance — so the item's underside rests ON the belt
    // line; lateral offset drifts gently back to center.
    private Quaternion rideRotation = Quaternion.identity;
    private Vector3 rideOffset;
    private float rideRestHeight;

    // how fast the lane follows sustained lateral displacement (wide-belt behavior:
    // shoves shift the item to a new lane instead of springing back to the old one)
    private const float laneAdoptSpeed = 1f;
    // rest must be SUSTAINED this long before capture — a bounce apex or a tumble has
    // near-zero velocity for a frame, and capturing there froze items at weird angles
    private const float settleDuration = 0.25f;
    private float settledUntil;

    [SerializeField] private Rigidbody rb;

    private (Vector3 pos, Vector3 tan) posTan;
    private PickupInteractable pickupInteractable;
    private Collider[] allColliders;

    private MoverState state = MoverState.OnBelt;
    private float resnapBlockedUntil;
    private float nextResnapCheck;

    public MoverState State => state;
    public bool IsOnBelt => state == MoverState.OnBelt && enabled;
    public Rigidbody Body => rb;

    // World-space velocity the belt drives this item at; zero when free.
    public Vector3 BeltVelocity => (state == MoverState.OnBelt && path != null && posTan.tan.sqrMagnitude > 1e-6f)
        ? posTan.tan.normalized * path.Speed
        : Vector3.zero;

    #region Unity Functions
    private void Start()
    {
        if (rb == null) rb = GetComponentInChildren<Rigidbody>();

        pickupInteractable = GetComponentInChildren<PickupInteractable>();

        // physical riding wants the item's REAL collision shape live in every state —
        // enable everything (box proxy + mesh become a compound; no more swapping)
        allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in allColliders)
        {
            if (c != null) c.enabled = true;
        }

        // Placement decides the starting state: an item sitting on any belt is captured
        // in place, keeping its placed pose. Anything else — away from every belt, or
        // over the belt's mass limit — starts life as a normal free prop.
        float radius = Mathf.Max(snapDistance, placementCaptureDistance);
        if (FindCapturePath(radius, out ConveyorPath startPath, out float startS))
        {
            path = startPath;
            Capture(startS, radius);
        }
        else
        {
            ReleaseFromConveyor();
        }
    }

    private void Update()
    {
        // while held, the hold owns the item entirely — never move or recapture it
        if (pickupInteractable != null && pickupInteractable.pickupActive) return;

        if (state != MoverState.Free) return;

        // per-frame rest tracking: any burst of motion pushes the settle window out —
        // bounce apexes and tumbles have near-zero velocity for single frames
        bool settled = rb != null && !rb.isKinematic
            && rb.linearVelocity.sqrMagnitude <= restSpeed * restSpeed
            && rb.angularVelocity.sqrMagnitude <= restAngularSpeed * restAngularSpeed;
        if (!settled) settledUntil = Time.time + settleDuration;

        TryResnap();
    }

    private void FixedUpdate()
    {
        if (state != MoverState.OnBelt) return;
        if (pickupInteractable != null && pickupInteractable.pickupActive) return;
        if (path == null || path.TotalLength <= 1e-4f || rb == null) return;

        float dt = Time.fixedDeltaTime;
        float L = path.TotalLength;
        float beltSpeed = path.Speed;
        bool beltLoop = path.Loop;

        var (basePos, baseTan) = path.SampleByDistanceSmoothed(s, tangentHalfWindow);
        posTan = (basePos, baseTan);
        Vector3 tanN = (baseTan.sqrMagnitude > 1e-6f) ? baseTan.normalized : transform.forward;

        // wide-belt riding: anywhere across the belt's width is a valid lane; only
        // past the physical edge (or off in height) does the item fall off
        float halfWidth = Mathf.Max(path.BeltWidth * 0.5f, snapDistance);
        Quaternion baseFrame = Quaternion.LookRotation(tanN, Vector3.up);
        Vector3 baseLocal = Quaternion.Inverse(baseFrame) * (rb.position - basePos);

        if (Mathf.Abs(baseLocal.x) > halfWidth + releaseDeviationPad
            || Mathf.Abs(baseLocal.y - rideOffset.y) > snapHeightTolerance)
        {
            ReleaseFromConveyor();
            resnapBlockedUntil = Time.time + resnapCooldown;
            ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.KnockedOff);
            return;
        }

        // The cursor advances at belt speed but stalls as the item falls behind its
        // LANE point (blocked, or physically unable to keep up). Lag is measured
        // against the lane — not the spine — so corner geometry works for edge lanes
        // too (an inner lane covers less world distance per unit of track; a
        // spine-based measure deadlocked edge riders at corners).
        float lag = Vector3.Dot((basePos + baseFrame * rideOffset) - rb.position, tanN);
        float advance = beltSpeed * dt * Mathf.Clamp01(1f - (lag - lagSlack) / lagSpan);
        s += advance;
        s = beltLoop ? Mathf.Repeat(s, L) : Mathf.Clamp(s, 0f, L);

        // end of a non-loop path -> fling off
        if (!beltLoop && s >= L - 1e-4f)
        {
            var (posEnd, tanEnd) = path.SampleByDistanceSmoothed(L, tangentHalfWindow);
            posTan = (posEnd, tanEnd);
            ReleaseFromConveyorWithForce();
            return;
        }

        // gravity-like settling: ease the ride pose so the item's nearest local axis
        // points straight up — a drum released at an angle flattens out while it
        // travels instead of gliding frozen on a corner. Heading (yaw) is preserved.
        Quaternion worldRide = baseFrame * rideRotation;
        Vector3 axUp = worldRide * Vector3.up;
        Vector3 axRight = worldRide * Vector3.right;
        Vector3 axFwd = worldRide * Vector3.forward;
        Vector3 bestAxis = axUp;
        float bestDot = axUp.y;
        if (-axUp.y > bestDot) { bestDot = -axUp.y; bestAxis = -axUp; }
        if (axRight.y > bestDot) { bestDot = axRight.y; bestAxis = axRight; }
        if (-axRight.y > bestDot) { bestDot = -axRight.y; bestAxis = -axRight; }
        if (axFwd.y > bestDot) { bestDot = axFwd.y; bestAxis = axFwd; }
        if (-axFwd.y > bestDot) { bestDot = -axFwd.y; bestAxis = -axFwd; }
        Quaternion settled = Quaternion.Inverse(baseFrame) * (Quaternion.FromToRotation(bestAxis, Vector3.up) * worldRide);
        rideRotation = Quaternion.RotateTowards(rideRotation, settled, tiltSettleDegPerSec * dt);

        // keep the rest height synced to the CURRENT orientation — as a tilted item
        // flattens, its pivot-to-underside shrinks (a stale height left it floating)
        bool hasBounds = false;
        Bounds itemBounds = default;
        foreach (var c in allColliders)
        {
            if (c == null || !c.enabled) continue;
            if (!hasBounds) { itemBounds = c.bounds; hasBounds = true; }
            else itemBounds.Encapsulate(c.bounds);
        }
        if (hasBounds) rideRestHeight = (rb.position.y - itemBounds.min.y) + rideHoverClearance;

        // ease the ride pose: drop height settles onto the surface; the lane follows
        // the item's actual lateral position (kept, never recentered)
        rideOffset.y = Mathf.MoveTowards(rideOffset.y, rideRestHeight, rideHeightSettleSpeed * dt);
        rideOffset.x = Mathf.MoveTowards(rideOffset.x, Mathf.Clamp(baseLocal.x, -halfWidth, halfWidth), laneAdoptSpeed * dt);

        // the target leads the cursor slightly so the feedforward always pulls ahead
        float lead = Mathf.Max(Mathf.Abs(beltSpeed) * dt * 2f, 0.02f);
        float sLead = beltLoop ? Mathf.Repeat(s + lead, L) : Mathf.Min(s + lead, L);
        var (pos, tan) = path.SampleByDistanceSmoothed(sLead, tangentHalfWindow);
        if (tan.sqrMagnitude <= 1e-6f) tan = tanN;
        tan.Normalize();
        Quaternion frame = Quaternion.LookRotation(tan, Vector3.up);
        Vector3 targetPos = pos + frame * rideOffset;
        Quaternion targetRot = frame * rideRotation;

        // SOFT velocity servo: belt-speed feedforward + gentle proportional pull
        // toward the target. A one-step full correction is infinitely stiff and
        // stick-slips against any persistent contact (floor, neighbors, the arm) —
        // physics still resolves contacts, so blocked items press, not phase
        Vector3 vel = tan * beltSpeed + (targetPos - rb.position) * posCorrectionGain;

        // belts move at ONE speed: cap the along-belt component so a freed item
        // resumes at pace instead of sprinting to catch up to its stalled target
        float along = Vector3.Dot(vel, tan);
        if (along > beltSpeed) vel -= tan * (along - beltSpeed);

        float maxVel = Mathf.Abs(beltSpeed) + maxCorrectionSpeed;
        if (vel.sqrMagnitude > maxVel * maxVel) vel = vel.normalized * maxVel;
        rb.linearVelocity = vel;

        Quaternion dq = targetRot * Quaternion.Inverse(rb.rotation);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x) && Mathf.Abs(angle) > 1e-3f)
        {
            float maxAng = maxTurnRateDegPerSec * Mathf.Deg2Rad;
            Vector3 angVel = axis.normalized * (angle * Mathf.Deg2Rad * angCorrectionGain);
            if (angVel.sqrMagnitude > maxAng * maxAng) angVel = angVel.normalized * maxAng;
            rb.angularVelocity = angVel;
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
        }
    }
    #endregion

    #region Helpers
    // Scans every enabled belt (ConveyorPath.All) for one this item could ride at its
    // current position: within the given horizontal radius of the spine, within height
    // tolerance, under the belt's mass limit, and not at a non-loop end. Nearest wins.
    private bool FindCapturePath(float radius, out ConveyorPath bestPath, out float bestS)
    {
        bestPath = null;
        bestS = 0f;
        float bestD2 = float.PositiveInfinity;

        var paths = ConveyorPath.All;
        for (int i = 0; i < paths.Count; i++)
        {
            ConveyorPath p = paths[i];
            if (p == null || p.TotalLength <= 1e-4f) continue;
            if (rb != null && p.MaxItemMass > 0f && rb.mass > p.MaxItemMass) continue;

            float sC = p.FindClosestS(transform.position);

            // a non-loop capture at the very end would re-fling instantly, forever
            if (!p.Loop && sC >= p.TotalLength - 0.05f) continue;

            Vector3 toItem = transform.position - p.PositionAtDistance(sC);
            if (Mathf.Abs(toItem.y) > snapHeightTolerance) continue;
            toItem.y = 0f;

            // a wide belt accepts items anywhere across its width, plus a margin so
            // an item resting right ON the edge line (pivot just past it) still rides
            float accept = Mathf.Max(radius, p.BeltWidth * 0.5f + captureEdgeMargin);
            float d2 = toItem.sqrMagnitude;
            if (d2 <= accept * accept && d2 < bestD2)
            {
                bestD2 = d2;
                bestPath = p;
                bestS = sC;
            }
        }
        return bestPath != null;
    }

    // Free-state recapture: only when the item has been settled (near-rest, not
    // tumbling) for a sustained window and is on/next to a belt, outside the
    // post-fling cooldown — so knocked-off items stay knocked off.
    private void TryResnap()
    {
        if (Time.time < resnapBlockedUntil || Time.time < nextResnapCheck) return;
        nextResnapCheck = Time.time + resnapCheckInterval;

        if (rb == null || rb.isKinematic) return;
        if (Time.time < settledUntil) return;

        if (FindCapturePath(snapDistance, out ConveyorPath bestPath, out float bestS))
        {
            path = bestPath;
            Capture(bestS, snapDistance);
        }
    }

    private void Capture(float sClosest, float maxLateralOffset)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
        }

        // measure pivot-to-underside from the live colliders so the item rides with
        // its underside on the belt line instead of its pivot buried at line height
        bool hasBounds = false;
        Bounds itemBounds = default;
        foreach (var c in allColliders)
        {
            if (c == null || !c.enabled) continue;
            if (!hasBounds) { itemBounds = c.bounds; hasBounds = true; }
            else itemBounds.Encapsulate(c.bounds);
        }
        rideRestHeight = hasBounds
            ? (transform.position.y - itemBounds.min.y) + rideHoverClearance
            : 0f;

        // capture the current pose relative to the belt frame: the item keeps the
        // orientation and lateral/height offset it landed with (offsets ease out)
        var (capturePos, captureTan) = path.SampleByDistanceSmoothed(sClosest, tangentHalfWindow);
        posTan = (capturePos, captureTan);
        if (captureTan.sqrMagnitude > 1e-6f)
        {
            Quaternion frame = Quaternion.LookRotation(captureTan, Vector3.up);
            rideRotation = Quaternion.Inverse(frame) * transform.rotation;

            Vector3 local = Quaternion.Inverse(frame) * (transform.position - capturePos);
            local.z = 0f;
            float halfW = Mathf.Max(path.BeltWidth * 0.5f, maxLateralOffset);
            local.x = Mathf.Clamp(local.x, -halfW, halfW);
            local.y = Mathf.Clamp(local.y, -snapHeightTolerance, snapHeightTolerance);
            rideOffset = local;
        }

        s = path.Loop ? Mathf.Repeat(sClosest, path.TotalLength) : Mathf.Clamp(sClosest, 0f, path.TotalLength);
        state = MoverState.OnBelt;

        ItemEvents.ReportBeltCaptured(this, path);
    }

    public void ReleaseFromConveyorWithForce()
    {
        if (rb == null) { Debug.Log($"[{name.ToUpper()}] has no rigidbody."); return; }

        rb.isKinematic = false;
        rb.useGravity = true;

        var (pos, tan) = posTan;

        if (tan.sqrMagnitude <= 1e-6f)
        {
            float back = Mathf.Max(0f, s - 0.05f);
            var (_, fallbackTan) = path.SampleByDistanceSmoothed(back, tangentHalfWindow);
            tan = (fallbackTan.sqrMagnitude > 1e-6f) ? fallbackTan : transform.forward;
        }

        rb.AddForce(tan.normalized * (path != null ? path.ExitForce : 3f), ForceMode.VelocityChange);

        resnapBlockedUntil = Time.time + resnapCooldown;
        state = MoverState.Free;

        ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.Flung);
    }

    public void ReleaseFromConveyor()
    {
        if (rb == null) { Debug.Log($"[{name.ToUpper()}] has no rigidbody."); return; }

        rb.isKinematic = false;
        rb.useGravity = true;

        state = MoverState.Free;
    }

    // Pickup hand-off: stop belt movement but do NOT touch the rigidbody — the hold
    // owns rigidbody state for the whole carry.
    public void DetachForPickup()
    {
        bool wasOnBelt = state == MoverState.OnBelt;

        state = MoverState.Free;

        if (wasOnBelt) ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.PickedUp);
    }

    // An explicit hit dislodges an on-belt item: release with physics and apply the
    // impulse. The cooldown stops the belt from instantly recapturing it.
    public void KnockOff(Vector3 impulse, Vector3 point)
    {
        if (state != MoverState.OnBelt) return;
        if (rb == null) return;

        ReleaseFromConveyor();
        resnapBlockedUntil = Time.time + resnapCooldown;
        rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);

        ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.KnockedOff);
    }
    #endregion
}
