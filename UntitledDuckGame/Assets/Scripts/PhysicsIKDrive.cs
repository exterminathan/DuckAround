using UnityEngine;

[RequireComponent(typeof(ConfigurableJoint))]
public class PhysicsIKDrive : MonoBehaviour
{
    [Tooltip("The Transform your TwoBoneIKConstraint moves to.")]
    public Transform ikTarget;

    ConfigurableJoint joint;

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

        joint.targetRotation = desiredLocal;
    }
}
