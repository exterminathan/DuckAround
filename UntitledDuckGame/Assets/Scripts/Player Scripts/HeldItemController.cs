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
    [Tooltip("Layer the item is moved to while held — tick exactly ONE. Must NOT be in the arm sweep masks (Prop is), or the sweeps clamp against the carried item and freeze the arm.")]
    public LayerMask heldItemLayer = 1 << 12; // Interactable

    [Header("Release")]
    [Tooltip("Seconds after release during which the arm/body cannot collide with the dropped item.")]
    public float armIgnoreSeconds = 0.3f;

    [Header("Mass Scale")]
    [Tooltip("Heaviest item mass the duck is expected to handle — the top anchor for BOTH the fling falloff and encumbrance mappings. Raise this when heavier props are authored.")]
    public float maxCarryMass = 250f;

    [Header("Fling")]
    [Tooltip("Seconds of recent hand motion averaged into the release velocity.")]
    public float flingSampleWindow = 0.12f;
    [Tooltip("Sampled hand velocity to launch velocity multiplier (applied before mass scaling).")]
    public float flingPowerScale = 1.2f;
    [Tooltip("Raw hand speed (m/s) below which release is a gentle place with zero velocity, so careful belt placement is unaffected.")]
    public float minFlingSpeed = 1.5f;
    [Tooltip("Launch speed cap (m/s) after all scaling. Also swallows single-frame spikes.")]
    public float maxFlingSpeed = 15f;
    [Range(0.01f, 0.99f)]
    [Tooltip("Fraction of the flick velocity an item AT Max Carry Mass keeps. Lighter items curve smoothly up toward 1 (momentum-style falloff).")]
    public float flingKeepAtMaxMass = 0.15f;
    [Tooltip("End-over-end spin per unit of launch speed (rad/s per m/s). 0 = no tumble.")]
    public float flingTumbleFactor = 0.5f;

    [Header("Encumbrance")]
    [Tooltip("Heavier held items slow duck movement, body yaw, and arm ease while carried.")]
    public bool enableEncumbrance = true;
    [Tooltip("Item mass at or below which carrying has no mobility penalty.")]
    public float encumberLightMass = 1f;
    [Range(0.05f, 1f)]
    [Tooltip("Move-speed multiplier at the heaviest mass. 1 = carrying never slows walking.")]
    public float minCarryMoveMobility = 0.8f;
    [Range(0.05f, 1f)]
    [Tooltip("Arm/yaw ease multiplier at the heaviest mass. 1 = carrying never slows aiming.")]
    public float minCarryArmMobility = 0.8f;

    private PlayerDuckController player;
    private Transform holdSlot;
    private CharacterController playerCC;
    private IsometricRaycaster arm;

    private PickupInteractable heldItem;
    private Rigidbody heldRb;
    private Collider[] heldCols;
    private int[] heldLayers;
    private Transform[] heldTransforms;
    private HeldItemHitForwarder hitForwarder;
    private RigidbodyInterpolation cachedInterpolation;
    private Rigidbody carryAnchor; // kinematic proxy a dangling carry hangs from (created once, reused)
    private Joint carryJoint;      // FixedJoint (rigid bite) or ConfigurableJoint (springy bite)
    private bool danglingCarry;
    private Coroutine restoreRoutine;
    private PickupInteractable lastReleasedItem;

    private State state = State.None;
    private float transitT;
    private Vector3 transitStartPos;
    private Quaternion transitStartRot;
    private Vector3 prevItemPos;

    private struct HandSample { public Vector3 pos; public float time; }
    private const int SampleCapacity = 32; // ~0.12 s of Held-state samples even at 240 fps
    private HandSample[] handSamples = new HandSample[SampleCapacity];
    private int sampleHead;
    private int sampleCount;

    public bool IsCarrying => state != State.None;

    // Found-or-added at runtime so no prefab wiring is required; refs come off the raycaster.
    // (The component also lives on the player prefab so tuning persists.)
    public static HeldItemController GetOrAdd(IsometricRaycaster arm) {
        var c = arm.GetComponent<HeldItemController>();
        if (c == null) c = arm.gameObject.AddComponent<HeldItemController>();
        c.player = arm.playerDuckController;
        c.holdSlot = arm.playerHoldSlot;
        c.arm = arm;
        if (c.player != null && c.playerCC == null) c.playerCC = c.player.GetComponent<CharacterController>();
        return c;
    }

    public void Grab(PickupInteractable item) {
        if (item == null || holdSlot == null) return;
        if (state != State.None) Release();

        heldRb = item.Body != null ? item.Body : item.GetComponent<Rigidbody>();
        if (heldRb == null) return;
        heldItem = item;

        // re-grabbing during the release grace window: cancel the pending ignore
        // restore, or it would fire mid-carry and un-ignore the arm vs the held item
        if (restoreRoutine != null && item == lastReleasedItem) StopCoroutine(restoreRoutine);

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
        int heldLayer = LayerFromMask(heldItemLayer);
        if (heldLayer < 0) Debug.LogWarning("[HeldItem] heldItemLayer mask is empty — item keeps its own layer; arm sweeps may clamp against it.");
        for (int i = 0; i < heldTransforms.Length; i++) {
            heldLayers[i] = heldTransforms[i].gameObject.layer;
            if (heldLayer >= 0) heldTransforms[i].gameObject.layer = heldLayer;
        }

        hitForwarder = item.gameObject.AddComponent<HeldItemHitForwarder>();
        hitForwarder.Init(player, heldCols);

        cachedInterpolation = heldRb.interpolation;
        danglingCarry = item.DanglingCarry;
        if (danglingCarry) {
            // ragdoll carry: the pelvis stays DYNAMIC and hangs from a kinematic anchor via
            // a FixedJoint so the joint chain keeps simulating and the limbs flail. Driving
            // the pelvis transform directly would rigidly drag the whole bone hierarchy
            // (the limbs are its transform children) and freeze the dangle.
            if (carryAnchor == null) {
                var anchorGO = new GameObject("CarryAnchor");
                carryAnchor = anchorGO.AddComponent<Rigidbody>();
                carryAnchor.isKinematic = true;
            }
            carryAnchor.gameObject.SetActive(true);
            // anchor pose chosen so that once it reaches the hold slot the pelvis sits at
            // slot.rotation * gripRotation (a FixedJoint preserves the creation-time offset)
            carryAnchor.transform.SetPositionAndRotation(
                heldRb.position,
                heldRb.rotation * Quaternion.Inverse(Quaternion.Euler(item.gripRotation)));
            float biteSpring = item.DanglingRotationSpring;
            if (biteSpring > 0f) {
                // springy bite: position locked to the bill, rotation spring-driven back to
                // the grip pose — the body sags and swings, then gets pulled upright
                var cj = heldRb.gameObject.AddComponent<ConfigurableJoint>();
                cj.connectedBody = carryAnchor;
                cj.xMotion = ConfigurableJointMotion.Locked;
                cj.yMotion = ConfigurableJointMotion.Locked;
                cj.zMotion = ConfigurableJointMotion.Locked;
                cj.angularXMotion = ConfigurableJointMotion.Free;
                cj.angularYMotion = ConfigurableJointMotion.Free;
                cj.angularZMotion = ConfigurableJointMotion.Free;
                cj.rotationDriveMode = RotationDriveMode.Slerp;
                cj.slerpDrive = new JointDrive {
                    positionSpring = biteSpring,
                    positionDamper = item.DanglingRotationDamper,
                    maximumForce = float.MaxValue
                };
                carryJoint = cj;
            }
            else {
                // rigid bite: orientation fully slaved to the bill
                var fj = heldRb.gameObject.AddComponent<FixedJoint>();
                fj.connectedBody = carryAnchor;
                carryJoint = fj;
            }
            heldRb.WakeUp();
        }
        else {
            // interpolation stamps the stale physics pose over transform writes — off while held
            heldRb.interpolation = RigidbodyInterpolation.None;
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
            heldRb.useGravity = false;
            heldRb.isKinematic = true;
        }

        transitStartPos = danglingCarry ? carryAnchor.transform.position : item.transform.position;
        transitStartRot = danglingCarry ? carryAnchor.transform.rotation : item.transform.rotation;
        prevItemPos = transitStartPos;
        sampleHead = 0;
        sampleCount = 0;
        transitT = 0f;
        state = State.Transit;
        SetEncumbrance(ComputeEncumbrance(item.CarryMass));

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
            if (danglingCarry) {
                if (carryJoint != null) Destroy(carryJoint);
            }
            else {
                heldRb.isKinematic = false;
                heldRb.useGravity = true;
                heldRb.interpolation = cachedInterpolation;
            }
            Vector3 fling = ComputeFlingVelocity(heldItem != null ? heldItem.CarryMass : heldRb.mass);
            heldRb.linearVelocity = fling;
            heldRb.angularVelocity = ComputeTumble(fling);
            // every release path lands here — multi-body carriers fling their remaining
            // rigidbodies and do their own bookkeeping (worker BT flags) in OnFlung
            if (heldItem != null) heldItem.OnFlung(fling);
        }
        if (carryAnchor != null) carryAnchor.gameObject.SetActive(false);
        carryJoint = null;
        danglingCarry = false;

        SetEncumbrance(0f);

        // player-ignores are already on from the carry — keep them through the grace
        // window so the arm/body can't punch the item as it falls, then restore
        if (heldCols != null) {
            lastReleasedItem = heldItem;
            restoreRoutine = StartCoroutine(RestorePlayerIgnores(heldCols));
        }

        heldItem = null;
        heldRb = null;
        heldCols = null;
        heldLayers = null;
        heldTransforms = null;
        hitForwarder = null;
    }

    private void AddSample(Vector3 pos) {
        handSamples[sampleHead].pos = pos;
        handSamples[sampleHead].time = Time.time;
        sampleHead = (sampleHead + 1) % SampleCapacity;
        if (sampleCount < SampleCapacity) sampleCount++;
    }

    // Average hand velocity across the sample window: newest sample vs the oldest still
    // inside it. Windowing (vs single-frame) survives hitches and mouse warps; the dead
    // zone keeps deliberate slow placement identical to a plain zero-velocity drop.
    private Vector3 ComputeFlingVelocity(float itemMass) {
        if (sampleCount < 2) return Vector3.zero;

        int newestIdx = (sampleHead - 1 + SampleCapacity) % SampleCapacity;
        HandSample newest = handSamples[newestIdx];
        HandSample oldest = newest;
        for (int i = 1; i < sampleCount; i++) {
            HandSample s = handSamples[(newestIdx - i + SampleCapacity) % SampleCapacity];
            if (newest.time - s.time > flingSampleWindow) break;
            oldest = s;
        }

        float dt = newest.time - oldest.time;
        if (dt <= 0.0001f) return Vector3.zero;

        Vector3 v = (newest.pos - oldest.pos) / dt;
        if (v.magnitude < minFlingSpeed) return Vector3.zero; // gentle place

        // momentum-style falloff anchored to the mass scale: an item at maxCarryMass keeps
        // exactly flingKeepAtMaxMass of the flick; lighter items curve smoothly toward 1
        float keep = Mathf.Clamp(flingKeepAtMaxMass, 0.01f, 0.99f);
        float armStrength = keep * Mathf.Max(1f, maxCarryMass) / (1f - keep);
        v *= flingPowerScale * (armStrength / (armStrength + Mathf.Max(0.01f, itemMass)));
        return Vector3.ClampMagnitude(v, maxFlingSpeed);
    }

    // End-over-end topspin around the axis perpendicular to the flight direction.
    private Vector3 ComputeTumble(Vector3 flingVelocity) {
        float speed = flingVelocity.magnitude;
        if (speed < 0.01f || flingTumbleFactor <= 0f) return Vector3.zero;
        Vector3 axis = Vector3.Cross(Vector3.up, flingVelocity / speed);
        if (axis.sqrMagnitude < 1e-4f) axis = holdSlot != null ? holdSlot.right : Vector3.right; // straight-up fling
        return axis.normalized * (speed * flingTumbleFactor);
    }

    // 0 = unencumbered, 1 = fully encumbered (at/above encumberHeavyMass).
    private float ComputeEncumbrance(float itemMass) {
        if (!enableEncumbrance) return 0f;
        return Mathf.InverseLerp(encumberLightMass, maxCarryMass, itemMass);
    }

    // Single write path for both mobility hooks so no exit path can leave them stuck.
    // t: 0 = unencumbered (both multipliers 1), 1 = heaviest (each channel at its own floor).
    private void SetEncumbrance(float t) {
        if (player != null) player.CarrySpeedMultiplier = Mathf.Lerp(1f, minCarryMoveMobility, t);
        if (arm != null) arm.ArmSpeedMultiplier = Mathf.Lerp(1f, minCarryArmMobility, t);
    }

    private void LateUpdate() {
        if (state == State.None) return;

        // item destroyed or slot lost mid-hold: nothing left to restore, just reset
        if (heldRb == null || holdSlot == null) {
            state = State.None;
            if (carryJoint != null) Destroy(carryJoint);
            if (carryAnchor != null) carryAnchor.gameObject.SetActive(false);
            carryJoint = null;
            danglingCarry = false;
            heldItem = null;
            heldRb = null;
            heldCols = null;
            heldLayers = null;
            heldTransforms = null;
            hitForwarder = null;
            SetEncumbrance(0f);
            return;
        }

        Vector3 gripOffset = heldItem != null ? heldItem.gripOffset : Vector3.zero;
        Vector3 gripEuler = heldItem != null ? heldItem.gripRotation : Vector3.zero;
        Vector3 targetPos = holdSlot.TransformPoint(gripOffset);
        // dangling carry: gripRotation is baked into the joint's relative pose, so the
        // anchor itself just tracks the slot
        Quaternion targetRot = danglingCarry ? holdSlot.rotation : holdSlot.rotation * Quaternion.Euler(gripEuler);
        // dangling carry drives the anchor and lets the joint pull the body after it
        Transform driven = danglingCarry && carryAnchor != null ? carryAnchor.transform : heldRb.transform;

        if (state == State.Transit) {
            transitT += Time.deltaTime / Mathf.Max(0.0001f, transitDuration);
            float k = Mathf.Clamp01(transitT);
            k = k * k * (3f - 2f * k); // smoothstep
            driven.position = Vector3.Lerp(transitStartPos, targetPos, k);
            driven.rotation = Quaternion.Slerp(transitStartRot, targetRot, k);
            if (transitT >= 1f) state = State.Held;
        }
        else {
            // hard lock, no smoothing — lag here reads as the item slipping out of the bill
            driven.position = targetPos;
            driven.rotation = targetRot;
        }

        // record hand motion for fling-on-release; Held only, so the transit tween
        // and pause frames (dt == 0) never contaminate the release velocity
        if (state == State.Held && Time.deltaTime > 0f) AddSample(heldRb.transform.position);

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

    // Index of the lowest layer ticked in the mask (the inspector field should have
    // exactly one selected); -1 if the mask is empty.
    private static int LayerFromMask(LayerMask mask) {
        for (int i = 0; i < 32; i++) {
            if ((mask.value & (1 << i)) != 0) return i;
        }
        return -1;
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
