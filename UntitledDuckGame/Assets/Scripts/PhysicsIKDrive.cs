using UnityEngine;

[RequireComponent(typeof(ConfigurableJoint))]
public class PhysicsIKDrive : MonoBehaviour
{
    [Tooltip("The Transform your TwoBoneIKConstraint moves to.")]
    public Transform ikTarget;

    ConfigurableJoint joint;


    [SerializeField][Min(.01f)] float rotationSmoothTime = 0.05f;
    [SerializeField][Min(0f)] float extraSecondsPerDegree = 0.0003f; // 0.0003*180°≈0.054s extra
    Quaternion rotationVelocity;

    void Start()
    {
        joint = GetComponent<ConfigurableJoint>();
    }

    void FixedUpdate()
    {
        // Compute desired rotation: 
        //   target’s world rotation → into this joint’s local space
        Quaternion worldToParent = Quaternion.Inverse(transform.parent.rotation);
        Quaternion desiredLocal = worldToParent * ikTarget.rotation;

        float angleDiff = Quaternion.Angle(joint.targetRotation, desiredLocal);
        float smooth = rotationSmoothTime + angleDiff * extraSecondsPerDegree;

        joint.targetRotation = Quaternion.Slerp(
        joint.targetRotation,
        desiredLocal,
        Time.fixedDeltaTime / smooth
    );

    }
}
