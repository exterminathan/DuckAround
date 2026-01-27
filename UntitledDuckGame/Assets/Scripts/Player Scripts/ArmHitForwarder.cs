using UnityEditor;
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
        //CURRENTLY being used for debugging what arms are hitting
        switch (other.gameObject.layer) {
            case 0: // Default
                Debug.Log("[AHF] OnCollisionEnter Default hit");
                break;

            case 3: // Player
                Debug.LogWarning($"[AHF] from: {this.transform.name} [WARNING] OnCollisionEnter Player Hit (shouldn't be possible)");
                break;
            case 7: // Prop
                Debug.Log($"[AHF] from: {this.transform.name}  OnCollisionEnter Prop hit");
                break;
            case 12: // Interactable
                Debug.Log($"[AHF] from: {this.transform.name}  OnCollisionEnter Interactable hit");
                break;
            case 15: // Wall
                Debug.Log($"[AHF] from: {this.transform.name}  OnCollisionEnter Wall hit");
                break;
            case 16: // Worker
                Debug.Log($"[AHF] from: {this.transform.name}  OnCollisionEnter Worker hit");
                other.collider.GetComponent<WorkerAIController>()?.audioAgent.Play("playerHitWorker");
                break;
            case 17: // Ragdoll
                Debug.Log($"[AHF] from: {this.transform.name}  OnCollisionEnter Ragdoll hit");
                break;


            default:
                Debug.Log($"[AHF] OnCollisionEnter hit unidentified layer");
                break;
        }


        var rb = other.collider.attachedRigidbody;
        Debug.Log(
            $"[AHF] HIT {other.gameObject.name} " +
            $"layer={LayerMask.LayerToName(other.gameObject.layer)} " +
            $"tag={other.gameObject.tag} " +
            $"hasRB={rb != null} isKinematic={(rb ? rb.isKinematic : false)}"
        );

        IsometricRaycaster.ShowDebugSphere(other.contacts[0].point, Color.red);
        
        //Collisions with npc
        var npc = other.collider.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);

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

