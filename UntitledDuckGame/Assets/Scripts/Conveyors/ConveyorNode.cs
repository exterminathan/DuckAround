using UnityEngine;

public class ConveyorNode : MonoBehaviour {
    public float cornerRadius = -1f;

    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.forward.normalized;

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(Position, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(Position, Position + Forward * 0.25f);
    }
#endif

}
