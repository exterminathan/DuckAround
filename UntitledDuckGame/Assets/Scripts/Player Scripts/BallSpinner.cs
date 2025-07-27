using UnityEngine;

public class BallSpinner : MonoBehaviour {
    [Header("Spin Settings")]
    [Tooltip("Degrees per second")]
    [SerializeField] private float spinSpeed = 720f;

    private Vector3 spinAxis;

    void Start() {
        spinAxis = Random.onUnitSphere;
    }

    void Update() {
        transform.Rotate(spinAxis * spinSpeed * Time.deltaTime, Space.Self);
    }
}
