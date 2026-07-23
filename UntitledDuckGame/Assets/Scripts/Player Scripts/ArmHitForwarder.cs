using UnityEngine;
public class ArmHitForwarder : MonoBehaviour {
    [SerializeField] private PlayerDuckController player;
    public Vector3 lastPos;
    public Vector3 velocity;

    private Rigidbody Mrb;

	void Awake() {
        Mrb = GetComponent<Rigidbody>();
        if (Mrb == null) {
            Mrb = GetComponentInParent<Rigidbody>();
        }
    }

	void Start() {
        lastPos = transform.position;
    }

    void OnCollisionEnter(Collision other) {
        // audio/ vfx for hit
        // change to be based on what is being hit
        if (other.gameObject.layer == 3) // Player
            Debug.LogWarning($"[AHF] from: {this.transform.name} [WARNING] OnCollisionEnter Player Hit (shouldn't be possible)");

        var rb = other.collider.attachedRigidbody;

        // on-screen hit marker
        IsometricRaycaster.ShowDebugSphere(other.contacts[0].point, Color.red, 0.5f);

        //Collisions with npc
        var npc = other.collider.GetComponent<WorkerAIController>();
        if (npc != null) {
            npc.SetStateAtValue("IsCollided", true);
            npc.audioAgent.Play("playerHitWorker");
        }

        // if regular prop rigidibody
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

            Debug.Log($"[AHF] {name} → {other.gameObject.name} " +
                $"(layer={LayerMask.LayerToName(other.gameObject.layer)}): " +
                $"impulse {impulse.magnitude:F3} (μ={μ:F2}, speed={speed:F2}) at {normal}");

            Vector3 point = other.contacts[0].point;
            rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

    }
}

