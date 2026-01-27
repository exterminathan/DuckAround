using UnityEngine;

public class DebugCameraMover : MonoBehaviour {
    [Header("Movement Settings")]
    public float moveSpeed = 10f;

    // In Isometric, horizontal movement usually looks "faster" visually, 
    // so we sometimes tweak this factor.
    public float horizontalSpeedFactor = 1f;

    private Vector3 isoForward;
    private Vector3 isoRight;

    void Start() {

        isoForward = Vector3.forward + Vector3.right;
        isoRight = Vector3.right + Vector3.back;

        isoForward.Normalize();
        isoRight.Normalize();
    }

    void Update() {
        Move();
    }

    void Move() {
        float h = -Input.GetAxisRaw("Horizontal");
        float v = -Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0, v);

        if (input.sqrMagnitude > 0) {
            input.Normalize();

            Vector3 dir = (isoForward * v) + (isoRight * h * horizontalSpeedFactor);

            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }
}