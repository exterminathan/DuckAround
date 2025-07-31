using UnityEngine;

public class FanRotator : MonoBehaviour {
    [SerializeField] private float fanSpeed = 2f;

    void Update() {
        transform.Rotate(Vector3.right, fanSpeed * Time.deltaTime);
    }
}
