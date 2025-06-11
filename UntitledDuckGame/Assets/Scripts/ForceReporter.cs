using UnityEngine;

public class ForceReporter : MonoBehaviour
{
    void OllisionEnter(Collision collision)
    {
        Rigidbody otherRb = collision.rigidbody;

        if (otherRb != null)
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            Vector3 collisionNormal = collision.contacts[0].normal;

            float impactStrength = Vector3.Dot(relativeVelocity, collisionNormal) * otherRb.mass;

            Debug.Log($"Impact force: {impactStrength} from object {collision.gameObject.name}");
        }       
    }
}
