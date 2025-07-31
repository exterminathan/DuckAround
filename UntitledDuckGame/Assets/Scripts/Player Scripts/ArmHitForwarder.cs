using UnityEngine;
public class ArmHitForwarder : MonoBehaviour {
    [SerializeField] private PlayerDuckController player;
    public Vector3 lastPos;
    public Vector3 velocity;

    void Start() {
        lastPos = transform.position;
    }

    void OnCollisionEnter(Collision other) {
        //Collisions with npc
        var npc = other.collider.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);

        var rb = other.collider.attachedRigidbody;
        if (rb != null && !rb.isKinematic) {

            Vector3 v = velocity;
            float speed = v.magnitude;

            if (speed < player.pushThreshold) return;

            // reduced mass: μ = m1*m2/(m1+m2)
            float m1 = player.robotMass;
            float m2 = rb.mass;

            float μ = m1 * m2 / (m1 + m2);

            Vector3 normal = other.contacts[0].normal;
            Vector3 impulse = v.normalized * speed * μ * player.armImpulseDampFactor;

            Debug.Log($"m1: {m1}");
            Debug.Log($"m2: {m2}");
            Debug.Log($" μ: {μ}");
            Debug.Log($"speed: {speed}");
            Debug.Log($"impulse: {impulse}");

            Debug.Log($"{name} → {other.gameObject.name}: impulse {impulse.magnitude:F5} at {normal}");

            Vector3 point = other.contacts[0].point;
            rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

    }
}

