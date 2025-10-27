using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMOD.Studio;



[RequireComponent(typeof(BehaviourTree))]
public class WorkerAIController : MonoBehaviour {

    [Header("State Path Settings")]
    public Waypoint StartWaypoint;
    public Waypoint TargetWaypoint;
    public float MoveSpeed = 3f;
    public float ChaseSpeed = 5f;
    public float ArriveThreshold = 0.1f;

    [Header("State Control Flags")]
    public bool IsAllowedToMove = true;
    public bool IsCollided = false;
    public bool IsRagdollActive = false;

    [Header("Animators")]
    [SerializeField] public Animator WorkerAnimator;
    [SerializeField] public Animator WorkerAlertAnimator;

    [Header("Player Detection")]
    [SerializeField] public float PlayerDetectionRange = 5f;
    [SerializeField] public float PlayerDetectionAngle = 45f;
    [SerializeField] public LayerMask PlayerDetectionLayerMask;
    [SerializeField] public Transform PlayerTransform;
    [SerializeField] public WorkerVisualController WorkerVisController;

    private BehaviourTree _tree;
    private Dictionary<string, object> _blackboard;

    [Header("Editor Debug")]
    public bool ragdollRBEnabled = false;
    private bool rgFlag = false;
    private Rigidbody[] rigidbodies;
    //colliders separated by type
    //because.. wait actually there is no reason
    //might be useful later and why not
    public Dictionary<Type, Collider[]> rigidbodyColliders { get; private set; } = new Dictionary<Type, Collider[]>();

    private GameObject originalFBX;
    public LayerMask workerCollisionLayerMask;

    [Header("Audio")]
    private EventInstance ragdollSound;

    

    [Header("Self")]
    [SerializeField] private Collider workerPrimaryCollider;

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

        };

        var workerAnim = WorkerAnimator;
        if (workerAnim != null)
            _blackboard["WorkerAnimator"] = workerAnim;

        var alertAnim = WorkerAlertAnimator;
        if (alertAnim != null)
            _blackboard["AlertAnimator"] = alertAnim;

        ApplyRagdoll(false);
        rgFlag = false;
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

        if (workerPrimaryCollider != null) {
            workerPrimaryCollider.enabled = !on;
        }

        if (!on) {
            if (originalFBX != null) {
                originalFBX.transform.localPosition = Vector3.zero;
                Debug.Log(originalFBX.name + $" position reset to {originalFBX.transform.localPosition}");
            }

            //turn on detection visual
            WorkerVisController.SetVisualColor(StateName.PATROL);
        }

    }

    // Collisions
    void OnCollisionEnter(Collision other) {
        if (other.contactCount > 0) {
            var hitPoint = other.contacts[0].point;
            Debug.Log($"worker collided with {other.gameObject.name} at {hitPoint}");
            Debug.DrawLine(hitPoint, hitPoint + new Vector3(0, 5, 0), Color.red, 20f);
            foreach (var c in other.contacts) {
                Debug.DrawRay(c.point, c.normal * 0.5f, Color.green, 20f);
            }

            _lastCollisionPoint = hitPoint;
            _hasCollisionPoint = true;
            Debug.DrawRay(hitPoint, Vector3.up * 0.25f, Color.red, 2f);
        }
        else {
            Debug.Log($"worker collided with {other.gameObject.name} (no contacts reported)");
        }

        if ((workerCollisionLayerMask & (1 << other.gameObject.layer)) != 0) {
            _blackboard["IsCollided"] = true;
        }
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
    private Waypoint FindClosestWaypoint(Vector3 pos) {
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

    private void OnValidate() {
#if UNITY_EDITOR
        if (Application.isPlaying && gameObject.scene.isLoaded) {
            if (TargetWaypoint != null) {
                Debug.Log("TargetWaypoint changed in editor: " + TargetWaypoint.name);
                UnityEditor.EditorApplication.delayCall += () => {
                    if (this != null) SetNewDestination(TargetWaypoint);
                };
            }
        }
#endif
    }
}
