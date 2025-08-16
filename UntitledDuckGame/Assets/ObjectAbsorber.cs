using UnityEngine;
using System.Collections;

public class ObjectAbsorber : MonoBehaviour {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    void OnCollisionEnter(Collision other) {
        StartCoroutine(ParentAfterDelay(other.transform));
    }

    private IEnumerator ParentAfterDelay(Transform child) {
        yield return new WaitForSeconds(2f);
        child.SetParent(transform);
    }
}
