using UnityEngine;
public class ArmHitForwarder : MonoBehaviour {
    [SerializeField] private PlayerDuckController player;
    public Vector3 lastPos;
    public Vector3 velocity;

    private Rigidbody Mrb;
    private BoxCollider box;
    private readonly Collider[] beltOverlapHits = new Collider[8];

	void Awake() {
        Mrb = GetComponent<Rigidbody>();
        if (Mrb == null) {
            Mrb = GetComponentInParent<Rigidbody>();
        }
        box = GetComponent<BoxCollider>();
    }

	void Start() {
        lastPos = transform.position;
    }

    // On-belt conveyor items are kinematic, and the arm rigidbodies are kinematic too —
    // kinematic-kinematic pairs never produce OnCollisionEnter, so belt knock-off is
    // detected with an explicit overlap query instead of collision callbacks.
    void Update() {
        CheckBeltKnockOff();
    }

    private void CheckBeltKnockOff() {
        if (box == null || player == null) return;

        float speed = velocity.magnitude;
        if (speed < player.pushThreshold) return;

        Vector3 center = transform.TransformPoint(box.center);
        Vector3 scale = transform.lossyScale;
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(box.size.x * scale.x),
            Mathf.Abs(box.size.y * scale.y),
            Mathf.Abs(box.size.z * scale.z)) * 0.5f;

        int n = Physics.OverlapBoxNonAlloc(center, halfExtents, beltOverlapHits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            var mover = beltOverlapHits[i].GetComponentInParent<ConveyorObjectMover>();
            if (mover == null || !mover.IsOnBelt) continue;

            Rigidbody itemRb = mover.Body;
            if (itemRb == null) continue;

            // reduced mass: μ = m1*m2/(m1+m2)
            float μ = player.robotMass * itemRb.mass / (player.robotMass + itemRb.mass);
            Vector3 impulse = velocity.normalized * speed * μ * player.armImpulseDampFactor;

            Debug.Log($"[AHF] {name} knocked {mover.name} off the belt (impulse {impulse.magnitude:F3})");
            mover.KnockOff(impulse, beltOverlapHits[i].ClosestPoint(center));
        }
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
