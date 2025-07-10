using UnityEngine;

[AddComponentMenu("IK/IK Collision Avoidance")]
public class IKCollisionAvoidance : MonoBehaviour
{
    [Tooltip("The Transform that your TwoBoneIKConstraint is targeting")]
    public Transform ikTarget;

    [Tooltip("How big a sphere to test for penetration")]
    public float probeRadius = 0.1f;

    [Tooltip("Which layers count as obstacles")]
    public LayerMask obstacleMask;

    [SerializeField][Min(0f)] float positionSmoothTime = 0.05f;
    Vector3 positionVelocity;

    void LateUpdate()
    {
        // Find any colliders overlapping the probe sphere
        Collider[] hits = Physics.OverlapSphere(ikTarget.position, probeRadius, obstacleMask);
        if (hits.Length == 0) return;

        Vector3 corrected = ikTarget.position;
        foreach (var hit in hits)
        {
            Vector3 closest = hit.ClosestPoint(ikTarget.position);
            Vector3 pushDir = (ikTarget.position - closest).normalized;
            corrected = closest + pushDir * probeRadius;
        }

        // SmoothDamp from current → corrected
        ikTarget.position = Vector3.SmoothDamp(
            ikTarget.position,
            corrected,
            ref positionVelocity,
            positionSmoothTime
        );
    }

    // Draw the probe in the editor
    void OnDrawGizmosSelected()
    {
        if (ikTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ikTarget.position, probeRadius);
        }
    }
}
