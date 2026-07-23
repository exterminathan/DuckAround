// HeldItemController.cs
using System.Collections;
using UnityEngine;

// Owns the held-item lifecycle: PickupTransit (tween to mouth) -> Held (follow) -> release.
// The item is never reparented — its world pose is driven here each LateUpdate (post-IK),
// so it never inherits rig-hierarchy scale and physics never fights the transform.
// While held the item's colliders STAY LIVE so it can shove props and hit workers
// (via HeldItemHitForwarder); self-interference is prevented with targeted collision
// ignores (arm + body) and a temporary layer swap that hides it from the arm sweeps.
public class HeldItemController : MonoBehaviour {
    private enum State { None, Transit, Held }

    [Header("Transit")]
    [Tooltip("Seconds for a grabbed item to fly from its grab pose into the mouth.")]
    public float transitDuration = 0.15f;

    [Header("Mouth Gape")]
    [Tooltip("Bill length (world units) used to convert item size into a mouth-opening angle.")]
    public float billLength = 0.35f;
    public float minGapeDegrees = 12f;
    public float maxGapeDegrees = 60f;
    public float mouthAnimDuration = 0.25f;

    [Header("Held Physics")]
    [Tooltip("Layer the item is moved to while held. Must NOT be in the arm sweep masks (Prop is), or the sweeps clamp against the carried item and freeze the arm. Interactable = 12.")]
    public int heldItemLayer = 12;

    [Header("Release")]
    [Tooltip("Seconds after release during which the arm/body cannot collide with the dropped item.")]
    public float armIgnoreSeconds = 0.3f;

    private PlayerDuckController player;
    private Transform holdSlot;
    private CharacterController playerCC;

    private PickupInteractable heldItem;
    private Rigidbody heldRb;
    private Collider[] heldCols;
    private int[] heldLayers;
    private Transform[] heldTransforms;
    private HeldItemHitForwarder hitForwarder;
    private RigidbodyInterpolation cachedInterpolation;

    private State state = State.None;
    private float transitT;
    private Vector3 transitStartPos;
    private Quaternion transitStartRot;
    private Vector3 prevItemPos;

    public bool IsCarrying => state != State.None;

    // Found-or-added at runtime so no prefab wiring is required; refs come off the raycaster.
    // (The component also lives on the player prefab so tuning persists.)
    public static HeldItemController GetOrAdd(IsometricRaycaster arm) {
        var c = arm.GetComponent<HeldItemController>();
        if (c == null) c = arm.gameObject.AddComponent<HeldItemController>();
        c.player = arm.playerDuckController;
        c.holdSlot = arm.playerHoldSlot;
        if (c.player != null && c.playerCC == null) c.playerCC = c.player.GetComponent<CharacterController>();
        return c;
    }

    public void Grab(PickupInteractable item) {
        if (item == null || holdSlot == null) return;
        if (state != State.None) Release();

        heldRb = item.Body != null ? item.Body : item.GetComponent<Rigidbody>();
        if (heldRb == null) return;
        heldItem = item;

        // conveyor hand-off first: it swaps colliders to the free config WITHOUT touching
        // the rigidbody, so everything below sees the carry-time collider set
        if (item.ConveyorMover != null) item.ConveyorMover.DetachForPickup();

        float gripSize = MeasureGripSize(item);

        // colliders stay LIVE (the item is a weapon); ignore only the player's own
        // arm + body so the carried item can't block or punch its carrier
        heldCols = item.GetComponentsInChildren<Collider>();
        SetPlayerIgnores(heldCols, true);

        // swap to a layer outside the arm sweep masks; original layers restored on release
        heldTransforms = item.GetComponentsInChildren<Transform>();
        heldLayers = new int[heldTransforms.Length];
        for (int i = 0; i < heldTransforms.Length; i++) {
            heldLayers[i] = heldTransforms[i].gameObject.layer;
            heldTransforms[i].gameObject.layer = heldItemLayer;
        }

        hitForwarder = item.gameObject.AddComponent<HeldItemHitForwarder>();
        hitForwarder.Init(player, heldCols);

        // interpolation stamps the stale physics pose over transform writes — off while held
        cachedInterpolation = heldRb.interpolation;
        heldRb.interpolation = RigidbodyInterpolation.None;
        heldRb.linearVelocity = Vector3.zero;
        heldRb.angularVelocity = Vector3.zero;
        heldRb.useGravity = false;
        heldRb.isKinematic = true;

        transitStartPos = item.transform.position;
        transitStartRot = item.transform.rotation;
        prevItemPos = transitStartPos;
        transitT = 0f;
        state = State.Transit;

        float gape = Mathf.Clamp(
            2f * Mathf.Atan2(gripSize * 0.5f, Mathf.Max(0.01f, billLength)) * Mathf.Rad2Deg,
            minGapeDegrees, maxGapeDegrees);
        if (player != null) player.OpenMouthTo(gape, mouthAnimDuration);
    }

    public void Release() {
        if (state == State.None) return;
        state = State.None;

        if (player != null) player.OpenMouthTo(0f, mouthAnimDuration);

        if (hitForwarder != null) Destroy(hitForwarder);

        if (heldTransforms != null) {
            for (int i = 0; i < heldTransforms.Length; i++) {
                if (heldTransforms[i] != null) heldTransforms[i].gameObject.layer = heldLayers[i];
            }
        }

        if (heldRb != null) {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
            heldRb.interpolation = cachedInterpolation;
        }

        // player-ignores are already on from the carry — keep them through the grace
        // window so the arm/body can't punch the item as it falls, then restore
        if (heldCols != null) StartCoroutine(RestorePlayerIgnores(heldCols));

        heldItem = null;
        heldRb = null;
        heldCols = null;
        heldLayers = null;
        heldTransforms = null;
        hitForwarder = null;
    }

    private void LateUpdate() {
        if (state == State.None) return;

        // item destroyed or slot lost mid-hold: nothing left to restore, just reset
        if (heldRb == null || holdSlot == null) {
            state = State.None;
            heldItem = null;
            heldRb = null;
            heldCols = null;
            heldLayers = null;
            heldTransforms = null;
            hitForwarder = null;
            return;
        }

        Vector3 gripOffset = heldItem != null ? heldItem.gripOffset : Vector3.zero;
        Vector3 targetPos = holdSlot.TransformPoint(gripOffset);
        Quaternion targetRot = holdSlot.rotation;

        if (state == State.Transit) {
            transitT += Time.deltaTime / Mathf.Max(0.0001f, transitDuration);
            float k = Mathf.Clamp01(transitT);
            k = k * k * (3f - 2f * k); // smoothstep
            heldRb.transform.position = Vector3.Lerp(transitStartPos, targetPos, k);
            heldRb.transform.rotation = Quaternion.Slerp(transitStartRot, targetRot, k);
            if (transitT >= 1f) state = State.Held;
        }
        else {
            // hard lock, no smoothing — lag here reads as the item slipping out of the bill
            heldRb.transform.position = targetPos;
            heldRb.transform.rotation = targetRot;
        }

        // feed the hit forwarder the item's real world velocity for impulse calculation
        if (hitForwarder != null && Time.deltaTime > 0f) {
            hitForwarder.velocity = (heldRb.transform.position - prevItemPos) / Time.deltaTime;
        }
        prevItemPos = heldRb.transform.position;
    }

    private float MeasureGripSize(PickupInteractable item) {
        if (item.gripSize > 0f) return item.gripSize;

        bool has = false;
        Bounds b = default;
        foreach (var col in item.GetComponentsInChildren<Collider>()) {
            if (!col.enabled) continue;
            if (!has) { b = col.bounds; has = true; }
            else b.Encapsulate(col.bounds);
        }
        return has ? b.size.y : 0.2f;
    }

    private IEnumerator RestorePlayerIgnores(Collider[] itemCols) {
        yield return new WaitForSeconds(armIgnoreSeconds);
        SetPlayerIgnores(itemCols, false);
    }

    // Ignores between the item and the player's arm colliders + CharacterController.
    // Unity auto-clears ignore pairs when a collider is disabled, so disabled ones are skipped.
    private void SetPlayerIgnores(Collider[] itemCols, bool ignore) {
        Collider[] armCols = player != null ? player.armColliders : null;

        foreach (var ic in itemCols) {
            if (ic == null || !ic.enabled || !ic.gameObject.activeInHierarchy) continue;

            if (armCols != null) {
                foreach (var ac in armCols) {
                    if (ac != null) Physics.IgnoreCollision(ic, ac, ignore);
                }
            }
            if (playerCC != null) Physics.IgnoreCollision(ic, playerCC, ignore);
        }
    }
}
