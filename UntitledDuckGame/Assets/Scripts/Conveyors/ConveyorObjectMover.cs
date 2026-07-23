using System;
using UnityEngine;

public class ConveyorObjectMover : MonoBehaviour
{
    public enum MoverState { OnBelt, Free }

    [Header("Object Mover Settings")]
    public ConveyorPath path;
    public float speed = 1f;
    public bool loop = true;

    public Quaternion initRotation;

    public float startOffset = 0f;
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

    private float s;
    private float nonLoopDistance;
    private bool wasLooping;

    private Quaternion initialLocalRotation;
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

    #region Unity Functions
    private void Start()
    {
        initialLocalRotation = initRotation;

        if (rb == null) rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        wasLooping = loop;
        nonLoopDistance = 0f;
        s = 0f;
        state = MoverState.OnBelt;

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
        else
        {
            interactionCollider.enabled = true;
            collCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (path == null || path.TotalLength <= 1e-4f) return;

        // while held, the hold owns the item entirely — never move or recapture it
        if (pickupInteractable != null && pickupInteractable.pickupActive) return;

        if (state == MoverState.Free)
        {
            TryResnap();
            return;
        }

        float L = path.TotalLength;
        float delta = speed * Time.deltaTime;

        if (loop)
        {
            s += delta;
            s = Mathf.Repeat(s, L);

            float param = Mathf.Repeat(s + startOffset, L);
            nonLoopDistance = param;

            SampleAt(param, delta);
            wasLooping = true;
        }
        else
        {
            if (wasLooping)
            {
                nonLoopDistance = Mathf.Repeat(s + startOffset, L);
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
                transform.position = posEnd;

                if (tanEnd.sqrMagnitude > 1e-6f)
                {
                    Quaternion target = Quaternion.LookRotation(tanEnd, Vector3.up) * initialLocalRotation;
                    transform.rotation = target;
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
        transform.position = pos;

        if (tan.sqrMagnitude > 1e-6f)
        {
            Quaternion target = Quaternion.LookRotation(tan, Vector3.up) * initialLocalRotation;

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
    }

    private float FindClosestSAlongPath(Vector3 worldPos)
    {
        float L = path.TotalLength;
        int N = Mathf.Max(64, Mathf.CeilToInt(L * 8f));
        float bestS = 0f;
        float bestD2 = float.PositiveInfinity;
        for (int i = 0; i <= N; i++)
        {
            float si = (L * i) / N;
            Vector3 pi = path.PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        float window = Mathf.Max(0.25f, L / N * 4f);
        int R = 24;
        for (int i = 0; i <= R; i++)
        {
            float si = Mathf.Clamp(bestS - window * 0.5f + window * (i / (float)R), 0f, L);
            Vector3 pi = path.PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        return bestS;
    }

    // Free-state recapture: only when the item is settled (near-rest) on/next to the
    // path and outside the post-fling cooldown, so knocked-off items stay knocked off.
    private void TryResnap()
    {
        if (Time.time < resnapBlockedUntil || Time.time < nextResnapCheck) return;
        nextResnapCheck = Time.time + resnapCheckInterval;

        if (rb == null || rb.isKinematic) return;
        if (rb.linearVelocity.sqrMagnitude > restSpeed * restSpeed) return;

        float sClosest = FindClosestSAlongPath(transform.position);
        if (Vector3.Distance(transform.position, path.PositionAtDistance(sClosest)) > snapDistance) return;

        Capture(sClosest);
    }

    private void Capture(float sClosest)
    {
        rb.isKinematic = true;
        rb.useGravity = false;

        if (interactionCollider != null) interactionCollider.enabled = true;
        if (collCollider != null) collCollider.enabled = false;

        float L = path.TotalLength;
        if (loop)
        {
            s = Mathf.Repeat(sClosest - startOffset, L);
            nonLoopDistance = Mathf.Repeat(s + startOffset, L);
            // deltaTime = 0f keeps current rotation this frame
            SampleAt(nonLoopDistance, 0f);
        }
        else
        {
            nonLoopDistance = Mathf.Clamp(sClosest, 0f, L);
            SampleAt(nonLoopDistance, 0f);
        }

        wasLooping = loop;
        state = MoverState.OnBelt;
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
        if (interactionCollider != null) interactionCollider.enabled = false;
        if (collCollider != null) collCollider.enabled = true;

        state = MoverState.Free;
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
    }
    #endregion
}
