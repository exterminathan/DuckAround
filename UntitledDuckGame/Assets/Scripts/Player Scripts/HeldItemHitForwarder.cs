// HeldItemHitForwarder.cs
using UnityEngine;

// Runtime-attached to a held item so the carried object acts as an extension of the arm:
// ragdolls workers on contact, forwards reduced-mass impulses to dynamic props, and knocks
// on-belt items off via overlap (on-belt items and the held item are both kinematic, and
// kinematic-kinematic pairs never produce collision callbacks).
// Added by HeldItemController on grab, destroyed on release.
public class HeldItemHitForwarder : MonoBehaviour {
    public Vector3 velocity; // fed by HeldItemController each LateUpdate

    private PlayerDuckController player;
    private Collider[] ownCols;
    // generous size: the everything-mask overlap near a belt also catches floor, belt
    // geometry, and the player's colliders — a small buffer can truncate the item out
    private readonly Collider[] beltOverlapHits = new Collider[32];

    public void Init(PlayerDuckController player, Collider[] itemCols) {
        this.player = player;
        ownCols = itemCols;
    }

    void Update() {
        CheckBeltKnockOff();
    }

    private void CheckBeltKnockOff() {
        if (player == null || ownCols == null) return;

        foreach (var c in ownCols) {
            if (c == null || !c.enabled) continue;

            Bounds b = c.bounds;
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, beltOverlapHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) {
                var mover = beltOverlapHits[i].GetComponentInParent<ConveyorObjectMover>();
                if (mover == null || !mover.IsOnBelt || mover.Body == null) continue;

                // RELATIVE contact speed: a held item parked across the path still
                // counts as a hit against an item the belt is carrying into it
                Vector3 relVel = velocity - mover.BeltVelocity;
                float relSpeed = relVel.magnitude;
                if (relSpeed < player.pushThreshold) continue;

                // reduced mass: μ = m1*m2/(m1+m2)
                float μ = player.robotMass * mover.Body.mass / (player.robotMass + mover.Body.mass);
                Vector3 impulse = relVel.normalized * relSpeed * μ * player.armImpulseDampFactor;
                mover.KnockOff(impulse, beltOverlapHits[i].ClosestPoint(b.center));
            }
        }
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
