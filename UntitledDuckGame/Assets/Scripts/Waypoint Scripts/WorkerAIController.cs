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

    private BehaviourTree _tree;
    private Dictionary<string, object> _blackboard;

    void Awake() {
        _tree = GetComponent<BehaviourTree>();
        _tree.Root = WaypointBTBuilder.CreateTree();

        _blackboard = new Dictionary<string, object> {
            ["SelfTransform"] = transform,
            ["StartWaypoint"] = StartWaypoint,
            ["TargetWaypoint"] = TargetWaypoint,
            ["Speed"] = MoveSpeed,
            ["ArriveThreshold"] = ArriveThreshold,
            ["IsAllowedToMove"] = IsAllowedToMove
        };

        var anim = GetComponentInChildren<Animator>();
        if (anim != null)
            _blackboard["Animator"] = anim;
    }

    void Update() {
        _blackboard["IsAllowedToMove"] = IsAllowedToMove;
        _tree.Root?.Execute(_blackboard);
    }

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
