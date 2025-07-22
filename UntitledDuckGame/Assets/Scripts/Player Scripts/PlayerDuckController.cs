using System.Collections;
using UnityEngine;

public class PlayerDuckController : MonoBehaviour {
    #region Rig & Camera Settings
    [Header("Rig Settings")]
    [SerializeField] private Transform rigTarget;
    [SerializeField] private Transform root;
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform meshBase;
    [SerializeField] private float quackRotation = 30f;
    [SerializeField] private float quackDuration = 0.1f;

    [Header("Camera/Perspective Settings")]
    public Transform isoCamera;
    private Vector3 isoForward;
    private Vector3 isoRight;
    #endregion

    #region Game & Movement Settings
    [Header("Game Settings")]
    [SerializeField] private bool isHoldingInMouth = false;
    [SerializeField] private bool isBrokenFree = false;
    [SerializeField] private LayerMask playerBlockingLayerMask;

    [Header("Movement Settings")]
    private bool canTraverse = true;
    private bool canFlex = true;
    [SerializeField] private float moveSpeed = 5f;
    [Range(0.5f, 2f)]
    [SerializeField] private float diagonalFactor = 1.85f;
    [SerializeField] private float dampeningFactor = 0.05f;
    [SerializeField] private float verticalMinClamp, verticalMaxClamp, horizontalMinClamp, horizontalMaxClamp;
    private int keysPressed = 0;
    private float rig_drop_distance = -0.14f;

    [Header("Movement Compensation")]
    [SerializeField] private float horizontalSpeedFactor = 1f;
    #endregion

    private Rigidbody rb;

    void Start() {
        if (isoCamera == null && Camera.main != null) isoCamera = Camera.main.transform;

        // Project camera forward onto XZ plane
        Vector3 f = isoCamera.forward;
        f.y = 0f;
        isoForward = f.normalized;
        // Perpendicular in XZ for “right”
        isoRight = Vector3.Cross(Vector3.up, isoForward).normalized;

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update() {
        if (isBrokenFree) {
            if (canFlex && canTraverse) {
                if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Quack());

                // Raw input so W=+1, S=-1, A=-1, D=+1
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");

                Vector3 input = new Vector3(h, 0f, v);
                if (input.sqrMagnitude > 0f) {
                    input.Normalize();
                    float speedMulti = (Mathf.Abs(input.x) > 0f && Mathf.Abs(input.z) > 0f)
                        ? diagonalFactor
                        : 1f;
                    // Map to isometric axes
                    Vector3 dir = isoForward * input.z + isoRight * (input.x * horizontalSpeedFactor);
                    Vector3 move = dir * moveSpeed * speedMulti * Time.deltaTime;
                    transform.Translate(move, Space.World);
                }
            }
        }
        else {
            // “Break-out” mash logic
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)) {
                keysPressed++;
                Debug.Log("Key pressed: " + keysPressed);
            }
            if (keysPressed > 15) {
                isBrokenFree = true;
                Debug.Log("You broke free!");
                meshBase.gameObject.SetActive(false);
                Debug.Log("Position before drop: " + transform.position);
                transform.Translate(0f, rig_drop_distance, 0f, Space.World);
                Debug.Log("Position after drop: " + transform.position);
            }
        }
    }

    private IEnumerator Quack() {
        float half = quackDuration * 0.5f;
        Quaternion start = mouth.localRotation;
        Quaternion open = start * Quaternion.Euler(0f, quackRotation, 0f);

        for (float t = 0f; t < half; t += Time.deltaTime) {
            mouth.localRotation = Quaternion.Lerp(start, open, t / half);
            yield return null;
        }
        mouth.localRotation = open;

        for (float t = 0f; t < half; t += Time.deltaTime) {
            mouth.localRotation = Quaternion.Lerp(open, start, t / half);
            yield return null;
        }
        mouth.localRotation = start;

        Debug.Log("Quack!");
    }
}
