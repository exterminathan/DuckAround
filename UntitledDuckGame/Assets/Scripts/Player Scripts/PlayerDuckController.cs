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
    private Quaternion mouthClosed;
    private Coroutine mouthRoutine;
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
    [Tooltip("Minimum contact speed (m/s) before arm/body/held-item hits apply impulses or knock items off belts.")]
    public float pushThreshold = 0.5f;

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
        if (mouth != null) mouthClosed = mouth.localRotation;

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
        if (Input.GetKeyDown(KeyCode.Space)) Quack();

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


    //only for collisions with base of quackbot
    // collisions for arms are handled by whatever is being hit (ref back to PlayerDuckController.audioAgent)
    void OnControllerColliderHit(ControllerColliderHit hit) {
        //if worker, set collided state for ragdoll
        var npc = hit.collider.GetComponent<WorkerAIController>();
        if (npc != null) npc.SetStateAtValue("IsCollided", true);

        // on-belt conveyor items are kinematic (no rigidbody impulse path) — a solid
        // body bump knocks them off the belt instead
        var mover = hit.collider.GetComponentInParent<ConveyorObjectMover>();
        if (mover != null && mover.IsOnBelt && mover.Body != null) {
            Vector3 beltHitVelocity = lastMoveDelta / Time.deltaTime;
            float beltVNorm = Vector3.Dot(beltHitVelocity, hit.normal);
            if (Mathf.Abs(beltVNorm) >= pushThreshold) {
                float beltMu = robotMass * mover.Body.mass / (robotMass + mover.Body.mass);
                mover.KnockOff(hit.normal * beltVNorm * beltMu * bodyImpulseDampFactor, hit.point);
            }
            return;
        }

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

    // Rotates the mouth to `degrees` of opening (Y axis) relative to its closed rest pose.
    // If duration > 0, lerps there over that many seconds; otherwise snaps instantly.
    private IEnumerator SetMouthOpen(float degrees, float duration) {
        // Lazily capture the closed rest pose. Guards against Hot Reload / domain
        // reloads where Start didn't run and mouthClosed is the zero quaternion.
        if (mouthClosed.x == 0f && mouthClosed.y == 0f && mouthClosed.z == 0f && mouthClosed.w == 0f)
            mouthClosed = mouth.localRotation;

        Quaternion start = mouth.localRotation;
        Quaternion target = mouthClosed * Quaternion.Euler(0f, -degrees, 0f);

        if (duration > 0f) {
            for (float t = 0f; t < duration; t += Time.deltaTime) {
                mouth.localRotation = Quaternion.Lerp(start, target, t / duration);
                yield return null;
            }
        }
        mouth.localRotation = target;
    }

    // Centralized mouth control: animates the mouth to `degrees` open over `duration`
    // (0 = snap), cancelling any in-progress mouth animation so callers (pickup
    // open/close, quack, etc.) never fight over mouth.localRotation.
    public void OpenMouthTo(float degrees, float duration) {
        if (mouthRoutine != null) StopCoroutine(mouthRoutine);
        mouthRoutine = StartCoroutine(SetMouthOpen(degrees, duration));
    }

    private void Quack() {
        float half = quackDuration * 0.5f;

        if (isometricRaycaster != null && isometricRaycaster.isHolding) {
            // Quacking while holding: drop the object. The held item may have opened
            // the mouth wider than the usual quack angle (scaled to its size), so we
            // skip the open "bump" and just close from the current rotation back to
            // the closed pose. Cancelling any active routine here supersedes the
            // close that EndHold -> OnHoldEnd kicks off, so they don't fight.
            isometricRaycaster.EndHold(this);

            // AUDIO: play the "quack while holding an object" clip here.

            if (mouthRoutine != null) StopCoroutine(mouthRoutine);
            mouthRoutine = StartCoroutine(SetMouthOpen(0f, half));
        }
        else {
            // Normal quack: open to the quack angle, then close.

            // AUDIO: play the "quack (empty mouth)" clip here.

            if (mouthRoutine != null) StopCoroutine(mouthRoutine);
            mouthRoutine = StartCoroutine(QuackRoutine(half));
        }

        Debug.Log("Quack!");
    }

    private IEnumerator QuackRoutine(float half) {
        yield return SetMouthOpen(quackRotation, half);
        yield return SetMouthOpen(0f, half);
    }

    public void ToggleMouth(bool openMouth, float duration) {
        OpenMouthTo(openMouth ? quackRotation / 2f : 0f, duration);
    }

}
