using UnityEngine;
using System;

[ExecuteAlways]
public class ConveyorNode : MonoBehaviour {
    public static event Action<ConveyorNode> OnAnyNodeMoved;
    public static event Action<ConveyorNode> OnDebugActivated;
    public static event Action<ConveyorNode> OnDebugDeactivated;

    public float cornerRadius = 0f;

    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.forward.normalized;

    public bool DebugOn = false;

#if UNITY_EDITOR
    private bool lastDebugOn;
    private void Update() {
        if (!Application.isPlaying && transform.hasChanged) {
            transform.hasChanged = false;
            OnAnyNodeMoved?.Invoke(this);
        }
    }

    private void OnValidate() {
        if (!Application.isPlaying) {
            OnAnyNodeMoved?.Invoke(this);
            if (DebugOn != lastDebugOn) {
                lastDebugOn = DebugOn;
                if (DebugOn)
                    OnDebugActivated?.Invoke(this);
                else
                    OnDebugDeactivated?.Invoke(this);
            }
        }
    }
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(Position, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(Position, Position + Forward * 0.25f);
    }
#endif

}
