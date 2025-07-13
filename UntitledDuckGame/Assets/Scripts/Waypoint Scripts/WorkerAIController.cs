using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BehaviourTree))]
public class WorkerAIController : MonoBehaviour {
    [Header("Path Settings")]
    public Waypoint StartWaypoint;
    public Waypoint TargetWaypoint;
    public float MoveSpeed = 3f;
    public float ArriveThreshold = 0.1f;

    [Header("Control Flags")]
    public bool IsAllowedToMove = true;
    public bool IsCollided = false;
    public bool IsRagdollActive = false;

    private BehaviourTree _tree;
    private Dictionary<string, object> _blackboard;

    void Awake() {
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
    }

    void Update() {
        _tree.Root?.Execute(_blackboard);
    }

    // Collisions
    void OnCollisionEnter(Collision other) {
        if (other.gameObject.layer == LayerMask.NameToLayer("AICollision")) {
            Destroy(other.gameObject);
            _blackboard["IsCollided"] = true;
        }
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
