using UnityEngine;

public class CubePropManager : MonoBehaviour {

    void OnCollisionEnter(Collision other) {
        if (other.gameObject.layer == LayerMask.NameToLayer("Worker")) {
            Debug.Log("Collide with worker");
            Destroy(gameObject);
        }
    }
}
