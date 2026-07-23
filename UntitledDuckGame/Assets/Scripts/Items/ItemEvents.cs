using System;
using UnityEngine;

// Central hub for item/world interaction events: belt capture/leave, free-item
// impacts (ground, props, belt frames), and the pickup lifecycle. Gameplay code
// reports here; audio/VFX routing subscribes here, so FMOD wiring lives in one
// router instead of scattered through gameplay logic. With logEvents on, every
// event prints with its payload so routing/tuning can be done from the console.
public static class ItemEvents {
    public enum BeltLeaveReason { PickedUp, KnockedOff, Flung }

    // item settled onto / was placed on a belt
    public static event Action<ConveyorObjectMover, ConveyorPath> BeltCaptured;
    // item left a belt (reason says how: grabbed, hit off, flung off the end)
    public static event Action<ConveyorObjectMover, BeltLeaveReason> BeltLeft;
    // free item collided with something; speed = collision.relativeVelocity magnitude
    public static event Action<PickupInteractable, Collision, float> ItemImpact;
    // pickup lifecycle (grab into mouth / released from mouth)
    public static event Action<PickupInteractable> PickedUp;
    public static event Action<PickupInteractable> Dropped;

    public static bool logEvents = true;

    public static void ReportBeltCaptured(ConveyorObjectMover item, ConveyorPath path) {
        if (logEvents) Debug.Log($"[ItemEvents] BeltCaptured: {item.name} -> {path.name}");
        BeltCaptured?.Invoke(item, path);
    }

    public static void ReportBeltLeft(ConveyorObjectMover item, BeltLeaveReason reason) {
        if (logEvents) Debug.Log($"[ItemEvents] BeltLeft ({reason}): {item.name}");
        BeltLeft?.Invoke(item, reason);
    }

    public static void ReportItemImpact(PickupInteractable item, Collision collision, float speed) {
        if (logEvents) Debug.Log($"[ItemEvents] ItemImpact: {item.name} hit {collision.collider.name} " +
            $"(layer={LayerMask.LayerToName(collision.collider.gameObject.layer)}) at {speed:F2} m/s");
        ItemImpact?.Invoke(item, collision, speed);
    }

    public static void ReportPickedUp(PickupInteractable item) {
        if (logEvents) Debug.Log($"[ItemEvents] PickedUp: {item.name}");
        PickedUp?.Invoke(item);
    }

    public static void ReportDropped(PickupInteractable item) {
        if (logEvents) Debug.Log($"[ItemEvents] Dropped: {item.name}");
        Dropped?.Invoke(item);
    }
}
