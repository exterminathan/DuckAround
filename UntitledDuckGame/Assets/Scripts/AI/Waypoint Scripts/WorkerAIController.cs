using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



[RequireComponent(typeof(BehaviourTree))]
public class WorkerAIController : MonoBehaviour {

    [Header("State Path Settings")]
    public Waypoint StartWaypoint;
    public Waypoint TargetWaypoint;
    public float MoveSpeed = 3f;
    public float ArriveThreshold = 0.1f;

    [Header("State Control Flags")]
    public bool IsAllowedToMove = true;
    public bool IsCollided = false;
    public bool IsRagdollActive = false;

    [Header("Animators")]
    [SerializeField] public Animator WorkerAnimator;
    [SerializeField] public Animator WorkerAlertAnimator;

    [Header("Audio")]
    public AudioAgent audioAgent;

    [Header("Player Detection")]

    [SerializeField] public LayerMask PlayerDetectionLayerMask;
    [SerializeField] public Transform PlayerTransform;
    [SerializeField] public WorkerVisualController WorkerVisController;

    private BehaviourTree _tree;
    private Dictionary<string, object> _blackboard;

    [Header("Editor Debug")]
    public bool ragdollRBEnabled = false;
    private Rigidbody[] rigidbodies;
    //colliders separated by type
    //because.. wait actually there is no reason
    //might be useful later and why not
    public Dictionary<Type, Collider[]> rigidbodyColliders { get; private set; } = new Dictionary<Type, Collider[]>();

    private GameObject originalFBX;
    public LayerMask workerCollisionLayerMask;

    [Header("Self")]
    [SerializeField] private Collider workerPrimaryCollider;

    [Header("Carry / Pickup")]
    [Tooltip("Player within this distance keeps a downed worker down, even after the recovery timer elapses.")]
    public float RecoverySuppressRange = 4f;
    [Tooltip("Pelvis speed (m/s) below which the ragdoll counts as settled enough to recover.")]
    public float RecoverySettleSpeed = 0.5f;
    [Tooltip("Midriff thickness fed to the mouth gape (0 would measure the whole body bounds).")]
    public float CarryGripSize = 0.4f;
    [Tooltip("Pelvis pivot offset from the bill anchor while carried, in hold-slot local space.")]
    public Vector3 CarryGripOffset = Vector3.zero;
    [Tooltip("Extra rotation (euler) on top of the bill rotation — tuned so the worker rides sideways.")]
    public Vector3 CarryGripRotation = new Vector3(0f, 0f, 90f);
    [Tooltip("Bite stiffness while carried: 0 = rigid grip; > 0 = spring-driven rotation, lower = the body sags/swings more in the mouth. Try 200 (floppy) to 2000 (firm).")]
    public float CarryRotationSpring = 800f;
    [Tooltip("Damping for the bite spring — higher kills swing/wobble faster.")]
    public float CarryRotationDamper = 40f;

    private Rigidbody pelvisRb;
    private WorkerPickupInteractable pickupAdapter;

    // --- Added: cache last collision point for gizmo drawing ---
    private Vector3 _lastCollisionPoint;
    private bool _hasCollisionPoint;


    void Awake() {

        // ragdoll section
        //assumes there are no children objects with colliders/rbs
        //EXCEEPT those that are on the rig
        //might be problematic if worker holding something/etc

        // all rigibodies except parent
        rigidbodies = GetComponentsInChildren<Rigidbody>().Where(rb => rb.gameObject != this.gameObject).ToArray();

        // all colliders except parent
        rigidbodyColliders[typeof(CapsuleCollider)] =
                        GetComponentsInChildren<CapsuleCollider>()
                        .Where(c => c.gameObject != this.gameObject).ToArray();

        rigidbodyColliders[typeof(BoxCollider)] = GetComponentsInChildren<BoxCollider>();
        rigidbodyColliders[typeof(SphereCollider)] = GetComponentsInChildren<SphereCollider>();

        //lowpoly_withrig fbx reference to reset position after ragdoll reset
        originalFBX = gameObject.transform.GetChild(0).gameObject;

        //carry/pickup: the pelvis is the one bone RB with no CharacterJoint — the ragdoll
        //root the player grabs; the adapter makes it a Pickup IInteractable (bone colliders
        //are already tagged Interactive in the prefab and only exist while ragdolled)
        pelvisRb = rigidbodies.FirstOrDefault(rb => rb.GetComponent<CharacterJoint>() == null);
        if (pelvisRb != null) {
            pickupAdapter = pelvisRb.gameObject.AddComponent<WorkerPickupInteractable>();
            pickupAdapter.Controller = this;
            pickupAdapter.BoneRigidbodies = rigidbodies;
            pickupAdapter.gripSize = CarryGripSize;
            pickupAdapter.gripOffset = CarryGripOffset;
            pickupAdapter.gripRotation = CarryGripRotation;
            pickupAdapter.PickupAllowed = false;
        }
        else {
            Debug.LogWarning($"[{name}] no joint-less bone rigidbody found — worker cannot be picked up");
        }


        //Tree stuff
        _tree = GetComponent<BehaviourTree>();
        _tree.Root = PrimaryBTBuilder.CreateTree();

        _blackboard = new Dictionary<string, object> {
            ["SelfTransform"] = transform,
            //animators
            ["AlertAnimator"] = WorkerAlertAnimator,
            //waypoints
            ["StartWaypoint"] = StartWaypoint,
            ["TargetWaypoint"] = TargetWaypoint,
            ["Speed"] = MoveSpeed,
            ["ArriveThreshold"] = ArriveThreshold,
            ["IsAllowedToMove"] = IsAllowedToMove,
            //ragdoll
            ["IsCollided"] = IsCollided,
            ["IsRagdollActive"] = IsRagdollActive,
            ["WorkerAIController"] = this,
            //player Detection
            ["IsChasing"] = false,
            ["LastDetectionTime"] = 0f,
            ["PlayerTransform"] = null,
            //carry/pickup (seeded so SetStateAtValue accepts external writes)
            ["IsHeld"] = false,
            ["PelvisRigidbody"] = pelvisRb,

        };


        var workerAnim = WorkerAnimator;
        if (workerAnim != null)
            _blackboard["WorkerAnimator"] = workerAnim;

        var alertAnim = WorkerAlertAnimator;
        if (alertAnim != null)
            _blackboard["AlertAnimator"] = alertAnim;

        ApplyRagdoll(false);
        _blackboard["IsRagdollActive"] = false;
    }


    void Update() {
        _tree.Root?.Execute(_blackboard);
    }

    public void ApplyRagdoll(bool on) {
        // toggle Animator (fetched from blackboard)
        if (_blackboard.TryGetValue("WorkerAnimator", out var a) && a is Animator animator) {
            animator.enabled = !on;
        }
        if (_blackboard.TryGetValue("AlertAnimator", out var aa) && aa is Animator alertAnimator) {
            alertAnimator.ResetTrigger("Activate");
            alertAnimator.SetTrigger("Deactivate");
        }


        // rigidbodies: non-kinematic when ragdoll==on
        foreach (var rb in rigidbodies) {
            rb.isKinematic = !on;
            rb.useGravity = on;
        }

        // colliders: enabled only in ragdoll mode
        foreach (var kvp in rigidbodyColliders) {
            foreach (var col in kvp.Value) {
                col.enabled = on;
            }
        }

        // play ragdoll audio
        if (on) { audioAgent.Play("ragdoll"); }

        // grabbable only while down (guards the click-on-recovery-frame race)
        if (pickupAdapter != null) pickupAdapter.PickupAllowed = on;

        if (workerPrimaryCollider != null) {
            workerPrimaryCollider.enabled = !on;
        }

        if (!on) {
            if (originalFBX != null) {
                originalFBX.transform.localPosition = Vector3.zero;
            }

            //turn on detection visual
            if (WorkerVisController != null) {
                WorkerVisController.SetVisualColor(StateName.PATROL);
            }
        }

    }

    // Collisions
    void OnCollisionEnter(Collision other) {
        if (other.contactCount > 0) {
            var hitPoint = other.contacts[0].point;


            //for some reason, armhitforwarder collisions arent logged
            //so call armhits sounds and etc from here

            //if hitpoint has armhitfowarder
            if (other.collider.GetComponent<ArmHitForwarder>() != null) {
                Debug.Log($"WorkerAIController collided with ArmHitForwarder on {other.gameObject.name}");
            }

            Debug.Log($"worker collided with {other.gameObject.name} at {hitPoint} with force {other.impulse.magnitude} in direction {other.impulse}");
            Debug.DrawLine(hitPoint, hitPoint + other.impulse, Color.yellow, 5f);

            foreach (var c in other.contacts) {
                Debug.DrawRay(c.point, c.normal * 0.5f, Color.green, 20f);
            }

            _lastCollisionPoint = hitPoint;
            _hasCollisionPoint = true;
            Debug.DrawRay(hitPoint, Vector3.up * 0.25f, Color.red, 2f);

            audioAgent.Play("ragdoll");

        }

        if ((workerCollisionLayerMask & (1 << other.gameObject.layer)) != 0) {
            _blackboard["IsCollided"] = true;
        }
    }

    public string GetData() {
        string outString = "";
        string targetName = (TargetWaypoint != null) ? TargetWaypoint.gameObject.name : "None";
        string modTargetName = $"{targetName[0]}{targetName[^1]}";
        outString += "WP: " + modTargetName + "\n";

        bool isChasing = _blackboard.ContainsKey("IsChasing") && (bool)_blackboard["IsChasing"];
        string alarmLevel = isChasing ? "A2" : "A1";
        outString += "AL: " + alarmLevel;

        return outString;

    }

    public void SetStateAtValue(string key, object newVal) {
        if (_blackboard.ContainsKey(key)) _blackboard[key] = newVal;
    }

    //small helper function for alert animation settings
    public void SetAlertAnimationActive(bool active) {
        if (_blackboard.TryGetValue("AlertAnimator", out var aa) && aa is Animator alertAnimator) {
            if (active) {
                alertAnimator.ResetTrigger("Deactivate");
                alertAnimator.SetTrigger("Activate");

            }
            else {
                alertAnimator.ResetTrigger("Activate");
                alertAnimator.SetTrigger("Deactivate");
            }
        }
    }

    //used by debug to set destination
    //
    public void SetNewDestination(Waypoint newTarget) {
        if (newTarget == null) {
            return;
        }
        TargetWaypoint = newTarget;
        _blackboard["TargetWaypoint"] = newTarget;

        // Reset path so it will FindPath once
        _blackboard.Remove("FullPath");

        // Update StartWaypoint to current position’s closest node
        Waypoint closest = FindClosestWaypoint(transform.position);
        StartWaypoint = closest;
        _blackboard["StartWaypoint"] = closest;
    }

    //local helper to find closest waypoint
    public Waypoint FindClosestWaypoint(Vector3 pos) {
        Waypoint[] all = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        Waypoint closest = null;
        float minDist = float.MaxValue;

        foreach (var w in all) {
            float d = Vector3.Distance(pos, w.transform.position);
            if (d < minDist) {
                minDist = d;
                closest = w;
            }
        }
        return closest;
    }

    // Gizmo for last collision point
    void OnDrawGizmosSelected() {
        if (_hasCollisionPoint) {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_lastCollisionPoint, 0.1f);
        }
    }

}
