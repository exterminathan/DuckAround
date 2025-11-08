using System;
using UnityEngine;

public class ConveyorObjectMover : MonoBehaviour
{

    [Header("Object Mover Settings")]
    public ConveyorPath path;
    public float speed = 1f;
    public bool loop = true;

    public Quaternion initRotation;

    public float startOffset = 0f;
    public float tangentHalfWindow = 0.25f;
    public float maxTurnRateDegPerSec = 720f;
    public float exitForce = 3f;

    private float s;
    private float nonLoopDistance;
    private bool wasLooping;

    private Quaternion initialLocalRotation;
    [SerializeField] private Rigidbody rb;

    private (Vector3 pos, Vector3 tan) posTan;
    private PickupInteractable pickupInteractable;
    private bool wasPickupActive;
    private Collider interactionCollider;
    private Collider collCollider;

    [SerializeField] private float snapDistance = 0.1f;

    // NEW: track that we re-enabled from a world drop
    private bool isReactivatingFromDrop;

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

        pickupInteractable = GetComponentInChildren<PickupInteractable>();
        Debug.Log($"{pickupInteractable} on start");
        wasPickupActive = false;

        interactionCollider = GetComponentInChildren<BoxCollider>();
        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
        }
        collCollider = GetComponentInChildren<MeshCollider>();
        if (collCollider != null)
        {
            collCollider.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Only reset when this is a "fresh" enable, NOT a world-drop reactivation
        if (!isReactivatingFromDrop)
        {
            transform.localRotation = initRotation;
            s = 0f;
            nonLoopDistance = 0f;
            wasLooping = loop;
        }

        if (pickupInteractable == null) pickupInteractable = GetComponent<PickupInteractable>();
        //Debug.Log($"{pickupInteractable} on enable");
        wasPickupActive = false;

        interactionCollider = GetComponentInChildren<BoxCollider>();
        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
        }
        collCollider = GetComponentInChildren<MeshCollider>();
        if (collCollider != null)
        {
            collCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (path == null || path.TotalLength <= 1e-4f) return;

        // If we were reactivated from a drop, snap to nearest point ONLY if close enough
        if (isReactivatingFromDrop)
        {
            isReactivatingFromDrop = false;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            float sClosest = FindClosestSAlongPath(transform.position);
            float Lsnap = path.TotalLength;

            Vector3 conveyorPos = path.PositionAtDistance(sClosest);
            float distToConveyor = Vector3.Distance(transform.position, conveyorPos);

            if (distToConveyor <= snapDistance)
            {
                if (loop)
                {
                    s = Mathf.Repeat(sClosest - startOffset, Lsnap);
                    nonLoopDistance = Mathf.Repeat(s + startOffset, Lsnap);
                    // deltaTime = 0f keeps current rotation this frame
                    SampleAt(nonLoopDistance, 0f);
                }
                else
                {
                    nonLoopDistance = Mathf.Clamp(sClosest, 0f, Lsnap);
                    SampleAt(nonLoopDistance, 0f);
                }
            }
            else
            {
                // Too far—detach again
                ReleaseFromConveyor();
            }
            // Do not advance in the same frame
            return;
        }

        // determine if object is currently picked up
        bool isPicked = pickupInteractable != null && pickupInteractable.pickupActive;
        if (isPicked)
        {
            wasPickupActive = true;
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            return;
        }

        if (wasPickupActive)
        {
            wasPickupActive = false;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            float sClosest = FindClosestSAlongPath(transform.position);
            float Lsnap = path.TotalLength;

            Vector3 conveyorPos = path.PositionAtDistance(sClosest);
            float distToConveyor = Vector3.Distance(transform.position, conveyorPos);

            if (distToConveyor <= snapDistance)
            {
                if (loop)
                {
                    s = Mathf.Repeat(sClosest - startOffset, Lsnap);
                    nonLoopDistance = Mathf.Repeat(s + startOffset, Lsnap);
                    SampleAt(nonLoopDistance, 0f);
                }
                else
                {
                    nonLoopDistance = Mathf.Clamp(sClosest, 0f, Lsnap);
                    SampleAt(nonLoopDistance, 0f);
                }
            }
            else
            {
                ReleaseFromConveyor();
            }
            return; // avoid advancing in the same frame we snapped
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

        enabled = false;
    }

    public void ReleaseFromConveyor()
    {
        if (rb == null) { Debug.Log($"[{name.ToUpper()}] has no rigidbody."); return; }

        rb.isKinematic = false;
        rb.useGravity = true;

        if (interactionCollider != null) interactionCollider.enabled = false;
        if (collCollider != null) collCollider.enabled = true;

        enabled = false;
    }

    // NEW: public reactivation entry that does not reset parameters
    public void ReactivateFromWorldDrop()
    {
        if (rb == null) rb = GetComponentInChildren<Rigidbody>();
        if (interactionCollider == null) interactionCollider = GetComponentInChildren<BoxCollider>();
        if (collCollider == null) collCollider = GetComponentInChildren<MeshCollider>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;

        }
        if (interactionCollider != null) interactionCollider.enabled = true;
        if (collCollider != null) collCollider.enabled = false;

        isReactivatingFromDrop = true;
        enabled = true;
    }
    #endregion
}
