using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
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

    private BehaviourTree _tree;
    private Dictionary<string, object> _blackboard;

    [Header("Editor Debug")]
    public bool ragdollRBEnabled = false;
    private bool rgFlag = false;
    private Rigidbody[] rigidbodies;
    private Dictionary<Type, Collider[]> rigidbodyColliders = new Dictionary<Type, Collider[]>();

    private GameObject originalFBX;


    void Awake() {
        // ragdoll testing
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
            ["StartWaypoint"] = StartWaypoint,
            ["TargetWaypoint"] = TargetWaypoint,
            ["Speed"] = MoveSpeed,
            ["ArriveThreshold"] = ArriveThreshold,
            ["IsAllowedToMove"] = IsAllowedToMove,
            ["IsCollided"] = IsCollided,
            ["IsRagdollActive"] = IsRagdollActive

        };

        var anim = GetComponentInChildren<Animator>();
        if (anim != null)
            _blackboard["Animator"] = anim;

        ApplyRagdoll(false);
        rgFlag = false;
        _blackboard["IsRagdollActive"] = false;
    }

    void Update() {
        // read the desired state from the blackboard
        bool desired = _blackboard.ContainsKey("IsRagdollActive")
                       && (bool)_blackboard["IsRagdollActive"];

        // only flip once when it actually changes
        if (desired != rgFlag) {
            ApplyRagdoll(desired);
            rgFlag = desired;
        }

        _tree.Root?.Execute(_blackboard);
    }

    private void ApplyRagdoll(bool on) {
        // toggle Animator (fetched from blackboard)
        if (_blackboard.TryGetValue("Animator", out var a) && a is Animator animator) {
            animator.enabled = !on;
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

        if (!on) {
            if (originalFBX != null) {
                originalFBX.transform.localPosition = Vector3.zero;
                Debug.Log(originalFBX.name + $" position reset to {originalFBX.transform.localPosition}");
            }
        }
    }

    // Collisions
    void OnCollisionEnter(Collision other) {
        if (other.gameObject.layer == LayerMask.NameToLayer("AICollision")) {
            Destroy(other.gameObject);
            _blackboard["IsCollided"] = true;
        }
        if (other.gameObject.layer == LayerMask.NameToLayer("Player")) {
            _blackboard["IsCollided"] = true;
        }
    }

    public void SetStateAtValue(string key, object newVal) {
        if (_blackboard.ContainsKey(key)) _blackboard[key] = newVal;
    }




    //used by debug to set destination
    //
    public void SetNewDestination(Waypoint newTarget) {
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
}
