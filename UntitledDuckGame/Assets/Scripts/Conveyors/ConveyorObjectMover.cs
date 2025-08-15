using UnityEngine;

public class ConveyorObjectMover : MonoBehaviour {
    public ConveyorPath path;
    public float speed = 1f;
    public bool loop = true;

    //curr dist travelled
    private float s;
    private Quaternion initialLocalRotation;

    private void Start() {
        initialLocalRotation = transform.localRotation;
    }

    private void Update() {
        if (path == null || path.TotalLength <= 1e-4f) return;

        s += speed * Time.deltaTime;

        if (loop) {
            s = Mathf.Repeat(s, path.TotalLength);
        }
        else {
            s = Mathf.Clamp(s, 0f, path.TotalLength);
        }

        var (pos, tan) = path.SampleByDistance(s);
        transform.position = pos;

        if (tan.sqrMagnitude > 1e-6f) {
            transform.rotation = Quaternion.LookRotation(tan, Vector3.up) * initialLocalRotation;
        }
    }
}
