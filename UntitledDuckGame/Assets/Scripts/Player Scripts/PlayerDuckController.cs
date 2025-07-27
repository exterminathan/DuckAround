using System.Collections;
using UnityEngine;
using UnityEngine.TextCore.Text;

[RequireComponent(typeof(CharacterController))]
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

    #region Collision Settings
    [Header("Collision Settings")]
    private CharacterController characterController;
    public Collider[] armColliders;
    private RaycastHit[] hitBuffer = new RaycastHit[1];
    #endregion



    void Start() {
        if (isoCamera == null && Camera.main != null) isoCamera = Camera.main.transform;
        var f = isoCamera.forward; f.y = 0; isoForward = f.normalized;

        // Perpendicular in XZ for “right”
        isoRight = Vector3.Cross(Vector3.up, isoForward).normalized;


        // gather controller + arm colliders
        characterController = GetComponent<CharacterController>();

        // prevent self‑collision
        foreach (var c in armColliders) Physics.IgnoreCollision(characterController, c, true);
    }

    void FixedUpdate() {
        if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Quack());

        if (!isBrokenFree) {
            // break‑out mash logic
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)) {
                keysPressed++;
                if (keysPressed > 15) {
                    isBrokenFree = true;
                    meshBase.gameObject.SetActive(false);
                    // small drop via CC
                    characterController.Move(new Vector3(0, rig_drop_distance, 0));
                }
            }
            return;
        }

        // normal movement
        if (canFlex && canTraverse) {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var input = new Vector3(h, 0, v);
            if (input.sqrMagnitude > 0) {
                input.Normalize();
                float speedMulti = (Mathf.Abs(input.x) > 0 && Mathf.Abs(input.z) > 0)
                    ? diagonalFactor : 1f;
                Vector3 dir = isoForward * input.z + isoRight * (input.x * horizontalSpeedFactor);
                Vector3 desiredMove = dir * moveSpeed * speedMulti * Time.fixedDeltaTime;

                // sweep each arm collider
                float maxDist = desiredMove.magnitude;
                Vector3 moveDir = desiredMove.normalized;
                foreach (var col in armColliders) {
                    if (!(col is BoxCollider box)) continue;

                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                    Quaternion orientation = box.transform.rotation;
                    Vector3 center = box.transform.TransformPoint(box.center);

                    if (Physics.BoxCast(center, halfExtents, moveDir, out RaycastHit hit, orientation, maxDist, playerBlockingLayerMask)) {
                        maxDist = Mathf.Min(maxDist, hit.distance);
                    }
                }

                // actually move
                characterController.Move(moveDir * maxDist);
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
