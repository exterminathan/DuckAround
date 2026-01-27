//PlayerDuckController.cs
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerDuckController : MonoBehaviour {
    #region Rig & Camera Settings
    [Header("Rig Settings")]
    [SerializeField] private Transform rigTarget;
    [SerializeField] private Transform root;
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform meshBase;
    [SerializeField] private IsometricRaycaster isometricRaycaster;
    [SerializeField] private float quackRotation = 30f;
    [SerializeField] private float quackDuration = 0.1f;

    [Header("Camera/Perspective Settings")]
    public Transform isoCamera;
    private Vector3 isoForward;
    private Vector3 isoRight;
    #endregion

    #region Game & Movement Settings
    [Header("Game Settings")]
    //[SerializeField] private bool isHoldingInMouth = false;
    [SerializeField] private bool isBrokenFree = false;
    [SerializeField] private LayerMask playerBlockingLayerMask;

    [Header("Movement Settings")]
    private bool canTraverse = false;
    private bool canFlex = true;
    [SerializeField] private float moveSpeed = 5f;
    [Range(0.5f, 2f)]
    [SerializeField] private float diagonalFactor = 1.85f;
    //[SerializeField] private float dampeningFactor = 0.05f;
    [SerializeField] private float verticalMinClamp, verticalMaxClamp, horizontalMinClamp, horizontalMaxClamp;
    private int keysPressed = 0;
    private float rig_drop_distance = -0.14f;
    private float vertVelocity = 0f;

    [Header("Physics Settings")]
    public float robotMass;
    public Vector3 lastMoveDelta { get; private set; }
    public float bodyImpulseDampFactor;
    public float armImpulseDampFactor;
    public float pushThreshold { get; set; }

    [Header("Movement Compensation")]
    [SerializeField] private float horizontalSpeedFactor = 1f;
    #endregion

    #region Collision Settings
    [Header("Collision Settings")]
    private CharacterController characterController;
    public Collider[] armColliders;
    private RaycastHit[] hitBuffer = new RaycastHit[1];

    #endregion

    #region Audio Settings
    [Header("Audio Settings")]
    public AudioAgent audioAgent;
    #endregion

    void Start() {
        if (isoCamera == null && Camera.main != null) isoCamera = Camera.main.transform;
        var f = isoCamera.forward; f.y = 0; isoForward = f.normalized;

        // Perpendicular in XZ for “right”
        isoRight = Vector3.Cross(Vector3.up, isoForward).normalized;


        // gather controller + arm colliders
        characterController = GetComponent<CharacterController>();
        characterController.skinWidth = 0.039f;

        // prevent self‑collision
        foreach (var c in armColliders) Physics.IgnoreCollision(characterController, c, true);
    }

    void Update() {
        // TODO:
        //quack - NEED TO ISOLATE
        if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Quack());

        // TODO: 
        // make spam keys only for tutorial
        if (!isBrokenFree) {
            // break‑out key spam 
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)) {
                keysPressed++;
                Debug.Log("Key pressed: " + keysPressed);
                if (keysPressed > 5) {
                    isBrokenFree = true;
                    meshBase.gameObject.SetActive(false);

                    canTraverse = true;
                    // small drop via CC
                    characterController.Move(new Vector3(0, rig_drop_distance, 0));

                }
            }
            return;
        }

        // normal movement
        if (canFlex && canTraverse && !isometricRaycaster.isInteracting) {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var input = new Vector3(h, 0, v);
            if (input.sqrMagnitude > 0) {
                input.Normalize();
                float speedMulti = (Mathf.Abs(input.x) > 0 && Mathf.Abs(input.z) > 0)
                    ? diagonalFactor : 1f;
                Vector3 dir = isoForward * input.z + isoRight * (input.x * horizontalSpeedFactor);
                Vector3 desiredMove = dir * moveSpeed * speedMulti * Time.deltaTime;

                // sweep each arm collider
                float maxDist = desiredMove.magnitude;
                Vector3 moveDir = desiredMove.normalized;

                // actually move
                lastMoveDelta = moveDir * maxDist;
                characterController.Move(lastMoveDelta);

                //drop down if not grounded
                vertVelocity = characterController.isGrounded ? -2f : vertVelocity - 9.81f * Time.deltaTime;
                characterController.Move(Vector3.up * vertVelocity * Time.deltaTime);
            }
        }
    }

    //Helper function for movement sweep for arms
    // to ensure they don't clip through player blocking layers / objects
    private void HandleMovementSweep() {
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;
        }
    }

    //only for collisions with base of quackbot
    // collisions for arms are handled by whatever is being hit (ref back to PlayerDuckController.audioAgent)
    void OnControllerColliderHit(ControllerColliderHit hit) {
        //if worker, set collided state for ragdoll
        var npc = hit.collider.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);

        Vector3 impulse = new Vector3(0, 0, 0);

        //if object has rigidbody, apply impulse
        Rigidbody otherRb = hit.rigidbody;
        if (otherRb != null && !otherRb.isKinematic) {
            Vector3 velocity = lastMoveDelta / Time.deltaTime;

            // calc normal
            Vector3 normal = hit.normal;
            float vNorm = Vector3.Dot(velocity, normal);
            if (Mathf.Abs(vNorm) < pushThreshold) return;

            // reduced mass: μ = m1*m2/(m1+m2)
            float m1 = robotMass;
            float m2 = otherRb.mass;

            float μ = m1 * m2 / (m1 + m2);


            //impulse: μ * vNorm * normal * dampFactor
            impulse = normal * vNorm * μ * bodyImpulseDampFactor;

            //Debug.Log($"{name} → {hit.gameObject.name}: impulse {impulse.magnitude} at {normal}");
            //apply at contact point
            otherRb.AddForceAtPosition(impulse, hit.point, ForceMode.Impulse);
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

    public IEnumerator ToggleMouth(bool openMouth, float duration) {
        Quaternion start = mouth.localRotation;
        Quaternion target = openMouth
            ? start * Quaternion.Euler(0f, quackRotation / 2, 0f)
            : start * Quaternion.identity;

        for (float t = 0f; t < duration; t += Time.deltaTime) {
            mouth.localRotation = Quaternion.Lerp(start, target, t / duration);
            yield return null;
        }
        mouth.localRotation = target;

    }

}
