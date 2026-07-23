using System;
using UnityEngine;

public class ConveyorObjectMover : MonoBehaviour
{
    public enum MoverState { OnBelt, Free }

    [Header("Object Mover Settings")]
    [Tooltip("The belt currently being ridden. Auto-assigned when a belt captures this item — no pre-wiring needed.")]
    public ConveyorPath path;
    public float speed = 1f;
    public bool loop = true;

    public float tangentHalfWindow = 0.25f;
    public float maxTurnRateDegPerSec = 720f;
    public float exitForce = 3f;

    [Header("Re-Snap Settings")]
    [SerializeField] private float snapDistance = 0.1f;
    [Tooltip("Item must be at/below this speed to be captured back onto the belt.")]
    [SerializeField] private float restSpeed = 0.35f;
    [Tooltip("Seconds after a fling/knock-off before the belt may recapture the item.")]
    [SerializeField] private float resnapCooldown = 0.5f;
    [SerializeField] private float resnapCheckInterval = 0.2f;
    [Tooltip("Max height above/below the path spine at which an item can still be captured.")]
    [SerializeField] private float snapHeightTolerance = 0.5f;

    // Placement tolerance for the initial on-belt check at Start: hand-placed scene
    // items rarely sit exactly on the spine, so this is laxer than snapDistance.
    private const float placementCaptureDistance = 0.5f;

    private float s;
    private float nonLoopDistance;
    private bool wasLooping;

    // Captured ride pose relative to the belt tangent frame, taken at every capture
    // (scene start or resnap): an item rides in whatever orientation and lateral
    // offset it was placed/dropped with. Height eases toward rideRestHeight — the
    // pivot-to-collider-bottom distance — so the item's underside rests ON the belt
    // line instead of its pivot sinking to it.
    private Quaternion rideRotation = Quaternion.identity;
    private Vector3 rideOffset;
    private float rideRestHeight;

    // captured drop height eases back onto the spine at this rate (units/sec)
    private const float rideHeightSettleSpeed = 1.5f;

    [SerializeField] private Rigidbody rb;

    private (Vector3 pos, Vector3 tan) posTan;
    private PickupInteractable pickupInteractable;
    private Collider interactionCollider;
    private Collider collCollider;

    private MoverState state = MoverState.OnBelt;
    private float resnapBlockedUntil;
    private float nextResnapCheck;

    public MoverState State => state;
    public bool IsOnBelt => state == MoverState.OnBelt && enabled;
    public Rigidbody Body => rb;

    // World-space velocity the belt imparts while riding; zero when free. The hit
    // forwarders use it for RELATIVE contact speed, so a stationary arm blocking
    // the path still registers the item running into it.
    public Vector3 BeltVelocity => (state == MoverState.OnBelt && posTan.tan.sqrMagnitude > 1e-6f)
        ? posTan.tan.normalized * speed
        : Vector3.zero;

    #region Unity Functions
    private void Start()
    {
        if (rb == null) rb = GetComponentInChildren<Rigidbody>();

        pickupInteractable = GetComponentInChildren<PickupInteractable>();

        // the box/mesh swap is only valid for items that have BOTH (box = on-belt
        // interaction proxy, mesh = free physics). Single-collider items (e.g. drum:
        // mesh only) must keep their collider live in every state — disabling their
        // only collider makes them unhoverable and lets the arm phase through.
        interactionCollider = GetComponentInChildren<BoxCollider>();
        collCollider = GetComponentInChildren<MeshCollider>();

        // a degenerate proxy box (e.g. an accidental 0.0001-size collider) would leave
        // the item effectively colliderless on the belt — treat it as absent instead
        if (interactionCollider is BoxCollider proxyBox)
        {
            Vector3 worldSize = Vector3.Scale(proxyBox.size, proxyBox.transform.lossyScale);
            if (Mathf.Abs(worldSize.x) < 0.02f || Mathf.Abs(worldSize.y) < 0.02f || Mathf.Abs(worldSize.z) < 0.02f)
            {
                interactionCollider = null;
            }
        }

        if (interactionCollider == null || collCollider == null)
        {
            interactionCollider = null;
            collCollider = null;
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

        // free items need no path — TryResnap scans every registered belt
        if (state == MoverState.Free)
        {
            TryResnap();
            return;
        }

        if (path == null || path.TotalLength <= 1e-4f) return;

        float L = path.TotalLength;
        float delta = speed * Time.deltaTime;

        if (loop)
        {
            s += delta;
            s = Mathf.Repeat(s, L);
            nonLoopDistance = s;

            SampleAt(s, delta);
            wasLooping = true;
        }
        else
        {
            if (wasLooping)
            {
                nonLoopDistance = s;
                wasLooping = false;
            }
            else
            {
                nonLoopDistance += delta;
            }

            if (nonLoopDistance >= L - 1e-4f)
            {
                float atEnd = L;
                var (posEnd, tanEnd) = path.SampleByDistanceSmoothed(atEnd, tangentHalfWindow);
                posTan = (posEnd, tanEnd);

                if (tanEnd.sqrMagnitude > 1e-6f)
                {
                    Quaternion frame = Quaternion.LookRotation(tanEnd, Vector3.up);
                    transform.position = posEnd + frame * rideOffset;
                    transform.rotation = frame * rideRotation;
                }
                else
                {
                    transform.position = posEnd + rideOffset;
                }

                ReleaseFromConveyorWithForce();
                return;
            }

            float clamped = Mathf.Clamp(nonLoopDistance, 0f, L);
            SampleAt(clamped, delta);
        }
    }
    #endregion

    #region Helpers
    private void SampleAt(float distanceAlongPath, float deltaTime)
    {
        var (pos, tan) = path.SampleByDistanceSmoothed(distanceAlongPath, tangentHalfWindow);
        posTan = (pos, tan);

        // ease the captured drop height toward resting the underside on the belt line
        rideOffset.y = Mathf.MoveTowards(rideOffset.y, rideRestHeight, rideHeightSettleSpeed * deltaTime);

        if (tan.sqrMagnitude > 1e-6f)
        {
            Quaternion frame = Quaternion.LookRotation(tan, Vector3.up);
            transform.position = pos + frame * rideOffset;

            Quaternion target = frame * rideRotation;

            if (maxTurnRateDegPerSec > 0f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    maxTurnRateDegPerSec * deltaTime
                );
            }
            else
            {
                transform.rotation = target;
            }
        }
        else
        {
            transform.position = pos + rideOffset;
        }
    }

    private float FindClosestSAlongPath(ConveyorPath p, Vector3 worldPos)
    {
        float L = p.TotalLength;
        int N = Mathf.Max(64, Mathf.CeilToInt(L * 8f));
        float bestS = 0f;
        float bestD2 = float.PositiveInfinity;
        for (int i = 0; i <= N; i++)
        {
            float si = (L * i) / N;
            Vector3 pi = p.PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        float window = Mathf.Max(0.25f, L / N * 4f);
        int R = 24;
        for (int i = 0; i <= R; i++)
        {
            float si = Mathf.Clamp(bestS - window * 0.5f + window * (i / (float)R), 0f, L);
            Vector3 pi = p.PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        return bestS;
    }

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

            float sC = FindClosestSAlongPath(p, transform.position);

            // a non-loop capture at the very end would re-fling instantly, forever
            if (!loop && sC >= p.TotalLength - 0.05f) continue;

            // horizontal ring around the spine + a separate height tolerance, so an
            // item resting ON the belt surface (pivot above the spine) still captures
            Vector3 toItem = transform.position - p.PositionAtDistance(sC);
            if (Mathf.Abs(toItem.y) > snapHeightTolerance) continue;
            toItem.y = 0f;

            float d2 = toItem.sqrMagnitude;
            if (d2 <= radius * radius && d2 < bestD2)
            {
                bestD2 = d2;
                bestPath = p;
                bestS = sC;
            }
        }
        return bestPath != null;
    }

    // Free-state recapture: only when the item is settled (near-rest) on/next to a
    // belt and outside the post-fling cooldown, so knocked-off items stay knocked off.
    private void TryResnap()
    {
        if (Time.time < resnapBlockedUntil || Time.time < nextResnapCheck) return;
        nextResnapCheck = Time.time + resnapCheckInterval;

        if (rb == null || rb.isKinematic) return;
        if (rb.linearVelocity.sqrMagnitude > restSpeed * restSpeed) return;

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
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // measure pivot-to-underside from the colliders live at capture time (free
        // config), BEFORE the swap below — a disabled collider has no valid bounds
        bool hasBounds = false;
        Bounds itemBounds = default;
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c == null || !c.enabled) continue;
            if (!hasBounds) { itemBounds = c.bounds; hasBounds = true; }
            else itemBounds.Encapsulate(c.bounds);
        }
        rideRestHeight = hasBounds ? transform.position.y - itemBounds.min.y : 0f;

        if (interactionCollider != null) interactionCollider.enabled = true;
        if (collCollider != null) collCollider.enabled = false;

        // capture the current pose relative to the belt frame: the item keeps the
        // orientation and lateral/height offset it landed with (height eases out)
        var (capturePos, captureTan) = path.SampleByDistanceSmoothed(sClosest, tangentHalfWindow);
        if (captureTan.sqrMagnitude > 1e-6f)
        {
            Quaternion frame = Quaternion.LookRotation(captureTan, Vector3.up);
            rideRotation = Quaternion.Inverse(frame) * transform.rotation;

            Vector3 local = Quaternion.Inverse(frame) * (transform.position - capturePos);
            local.z = 0f;
            local.x = Mathf.Clamp(local.x, -maxLateralOffset, maxLateralOffset);
            local.y = Mathf.Clamp(local.y, -snapHeightTolerance, snapHeightTolerance);
            rideOffset = local;
        }

        float L = path.TotalLength;
        s = loop ? Mathf.Repeat(sClosest, L) : Mathf.Clamp(sClosest, 0f, L);
        nonLoopDistance = s;
        // deltaTime = 0f keeps current rotation this frame
        SampleAt(nonLoopDistance, 0f);

        wasLooping = loop;
        state = MoverState.OnBelt;

        ItemEvents.ReportBeltCaptured(this, path);
    }

    public void ReleaseFromConveyorWithForce()
    {
        if (rb == null) { Debug.Log($"[{name.ToUpper()}] has no rigidbody."); return; }

        rb.isKinematic = false;
        rb.useGravity = true;

        if (interactionCollider != null) interactionCollider.enabled = false;
        if (collCollider != null) collCollider.enabled = true;

        var (pos, tan) = posTan;

        if (tan.sqrMagnitude <= 1e-6f)
        {
            float back = Mathf.Max(0f, nonLoopDistance - 0.05f);
            var (_, fallbackTan) = path.SampleByDistanceSmoothed(back, tangentHalfWindow);
            tan = (fallbackTan.sqrMagnitude > 1e-6f) ? fallbackTan : transform.forward;
        }

        rb.AddForce(tan.normalized * exitForce, ForceMode.VelocityChange);

        resnapBlockedUntil = Time.time + resnapCooldown;
        state = MoverState.Free;

        ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.Flung);
    }

    public void ReleaseFromConveyor()
    {
        if (rb == null) { Debug.Log($"[{name.ToUpper()}] has no rigidbody."); return; }

        rb.isKinematic = false;
        rb.useGravity = true;

        if (interactionCollider != null) interactionCollider.enabled = false;
        if (collCollider != null) collCollider.enabled = true;

        state = MoverState.Free;
    }

    // Pickup hand-off: swap to the free collider config and stop belt movement, but do
    // NOT touch the rigidbody — the hold owns rigidbody state for the whole carry.
    public void DetachForPickup()
    {
        bool wasOnBelt = state == MoverState.OnBelt;

        if (interactionCollider != null) interactionCollider.enabled = false;
        if (collCollider != null) collCollider.enabled = true;

        state = MoverState.Free;

        if (wasOnBelt) ItemEvents.ReportBeltLeft(this, ItemEvents.BeltLeaveReason.PickedUp);
    }

    // An arm/body hit dislodges an on-belt item: release with physics and apply the
    // hit impulse. The cooldown stops the belt from instantly recapturing it.
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
