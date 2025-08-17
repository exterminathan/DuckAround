using UnityEngine;
using System.Collections;
using JetBrains.Annotations;

public class ObjectAbsorber : MonoBehaviour {

    private Quaternion initRotation;
    public LayerMask ignoredLayer;

    private Collider parentCollider;

    void Start() {
        initRotation = transform.rotation;
        parentCollider = GetComponent<Collider>();

    }

    void Update() {
        if (transform.hasChanged) {
            for (int h = transform.childCount - 1; h >= 0; h--) {
                Transform child = transform.GetChild(h);
                //if child not within bounds of parent, unparent it
                Collider childCol = child.GetComponent<Collider>();

                Vector3 checkPos = childCol != null ? childCol.bounds.center : child.position;
                if (!parentCollider.bounds.Contains(checkPos)) {
                    child.SetParent(null);
                }


            }
            if (Quaternion.Angle(initRotation, transform.rotation) >= 135f) {

                for (int i = transform.childCount - 1; i >= 0; i--) {
                    Transform child = transform.GetChild(i);
                    Debug.Log($"Unparenting {child.name} from {transform.name}");

                    Renderer childRenderer = child.GetComponentInChildren<Renderer>();
                    if (childRenderer != null) {
                        StartCoroutine(FadeEmission(childRenderer, 0f, 2.5f, 1f)); // fade in
                    }

                    child.SetParent(null);
                }
            }

            transform.hasChanged = false;
        }
    }

    void OnCollisionEnter(Collision other) {
        if ((ignoredLayer & (1 << other.gameObject.layer)) != 0) {
            Debug.Log($"Collision with player: {other.transform.name}");
        }
        else {
            StartCoroutine(ParentAfterDelay(other.transform));
        }
    }

    private IEnumerator ParentAfterDelay(Transform child) {
        yield return new WaitForSeconds(2f);

        child.SetParent(transform);

        Renderer childRenderer = child.GetComponentInChildren<Renderer>();
        if (childRenderer != null) {
            StartCoroutine(FadeEmission(childRenderer, 2.5f, 0f, 1f)); // fade out
        }
    }

    private IEnumerator FadeEmission(Renderer rend, float startIntensity, float endIntensity, float duration) {
        Material mat = rend.material;
        mat.EnableKeyword("_EMISSION");

        Color baseColor = mat.GetColor("_EmissionColor");
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Lerp(startIntensity, endIntensity, t);

            mat.SetColor("_EmissionColor", baseColor * intensity);
            yield return null;
        }

        if (endIntensity <= 0f) {
            mat.DisableKeyword("_EMISSION");
        }
    }
}
