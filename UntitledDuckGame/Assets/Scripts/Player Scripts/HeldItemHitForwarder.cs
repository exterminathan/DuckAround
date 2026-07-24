// HeldItemHitForwarder.cs
using UnityEngine;

// Runtime-attached to a held item so the carried object acts as an extension of the
// arm: ragdolls workers on contact and forwards reduced-mass impulses to dynamic
// bodies — including on-belt items, which now ride as dynamic bodies themselves, so
// ordinary collision callbacks cover them (the old overlap queries are gone).
// Added by HeldItemController on grab, destroyed on release.
public class HeldItemHitForwarder : MonoBehaviour {
    public Vector3 velocity; // fed by HeldItemController each LateUpdate

    private PlayerDuckController player;

    public void Init(PlayerDuckController player, Collider[] itemCols) {
        this.player = player;
    }

    void OnCollisionEnter(Collision other) {
        if (player == null) return;

        //Collisions with npc
        var npc = other.collider.GetComponent<WorkerAIController>();
        if (npc != null) {
            npc.SetStateAtValue("IsCollided", true);
            npc.audioAgent.Play("playerHitWorker");
        }

        // if regular prop rigidbody
        var rb = other.collider.attachedRigidbody;
        if (rb != null && !rb.isKinematic) {
            float speed = velocity.magnitude;
            if (speed < player.pushThreshold) return;

            // reduced mass: μ = m1*m2/(m1+m2)
            float μ = player.robotMass * rb.mass / (player.robotMass + rb.mass);
            Vector3 impulse = velocity.normalized * speed * μ * player.armImpulseDampFactor;

            Debug.Log($"[HeldItem] {name} → {other.gameObject.name}: impulse {impulse.magnitude:F3} (speed={speed:F2})");
            rb.AddForceAtPosition(impulse, other.contacts[0].point, ForceMode.Impulse);
        }
    }
}
