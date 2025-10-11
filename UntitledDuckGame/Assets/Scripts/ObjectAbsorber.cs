using UnityEngine;
using System.Collections;
using JetBrains.Annotations;

public class ObjectAbsorber : MonoBehaviour {

    private Quaternion initRotation;
    public LayerMask ignoredLayer;

    private Collider[] parentColliders;

    private Transform childCollector;

    void Start() {
        initRotation = transform.rotation;
        parentColliders = GetComponentsInChildren<Collider>();

        // get child of transform that contains child object with ChildCollector component (to avoid name based search)
        childCollector = gameObject.transform;

    }

    void Update() {
        if (childCollector.hasChanged) {
            for (int h = childCollector.childCount - 1; h >= 0; h--) {
                Transform child = childCollector.GetChild(h);
                //if child not within bounds of parent, unparent it
                Collider childCol = child.GetComponent<Collider>();

                Vector3 checkPos = childCol != null ? childCol.bounds.center : child.position;
                Bounds parentBounds = GetCombinedBounds(parentColliders);
                if (!parentBounds.Contains(checkPos)) {
                    child.SetParent(null);
                }


            }
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt >= 135f) {

                for (int i = childCollector.childCount - 1; i >= 0; i--) {
                    Transform child = childCollector.GetChild(i);
                    Debug.Log($"Unparenting {child.name} from {childCollector.name}");

                    Renderer childRenderer = child.GetComponentInChildren<Renderer>();
                    if (childRenderer != null) {
                        StartCoroutine(FadeEmission(childRenderer, 0f, 2.5f, 1f)); // fade in
                    }

                    child.SetParent(null);
                }
            }

            childCollector.hasChanged = false;
        }
    }

    void OnCollisionEnter(Collision other) {
        if ((ignoredLayer & (1 << other.gameObject.layer)) != 0) {
            Debug.Log($"Collision with: {other.transform.name}");
        }
        else {
            StartCoroutine(ParentAfterDelay(other.transform));
        }
    }

    private IEnumerator ParentAfterDelay(Transform child) {
        yield return new WaitForSeconds(2f);

        child.SetParent(childCollector);

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

    private Bounds GetCombinedBounds(Collider[] colliders) {
        if (colliders == null || colliders.Length == 0) {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds combinedBounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++) {
            combinedBounds.Encapsulate(colliders[i].bounds);
        }
        return combinedBounds;
    }
}
