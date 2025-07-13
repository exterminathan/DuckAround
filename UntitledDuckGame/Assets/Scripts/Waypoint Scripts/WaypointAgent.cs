// DEPRECATED
// USE WORKER AI CONTROLLER FOR WAYPOINT IMPLEMENTATION
// CAN STILL BE USED FOR NON-BT OBJECTS THAT NAVIGATE ON THE WAYPOINTS
// BUT HAVE TO CHANGE ANIMATION STUFF

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WaypointAgent : MonoBehaviour {
    [Tooltip("Ordered list of waypoints to visit; element 0 is the start, last is final destination")]
    public List<Waypoint> destinations = new List<Waypoint>();
    [Tooltip("Units per second")]
    public float speed = 3f;

    public float rotationSpeed = 720f;
    [Tooltip("If true, when you reach the last destination you'll loop back to the first endlessly")]
    public bool canLoop = false;

    public Animator workerAnimator;

    private Rigidbody rb;
    private List<Waypoint> path;
    private int currentDestIndex;
    private int pathIndex;
    private bool _isMoving;

    private bool isMoving {
        get { return _isMoving; }
        set {
            if (_isMoving != value) {
                _isMoving = value;
                if (workerAnimator != null) {
                    workerAnimator.SetBool("isWalking", _isMoving);
                }
            }
        }
    }

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    void Start() {
        if (destinations == null || destinations.Count < 2) {
            Debug.LogError("WaypointAgent: assign at least two waypoints in ‘destinations’.", this);
            enabled = false;
            return;
        }

        // snap agent to the very first waypoint
        var startWP = destinations[0];
        rb.position = new Vector3(startWP.transform.position.x, rb.position.y, startWP.transform.position.z);

        currentDestIndex = 1;
        ComputePathSegment();
    }

    void Update() {
        // if we've stopped at end and looping got turned on, restart
        if (!isMoving && canLoop) {
            Debug.Log("WaypointAgent: restarting loop.", this);
            var startWP = destinations[0];
            rb.position = new Vector3(startWP.transform.position.x, rb.position.y, startWP.transform.position.z);
            currentDestIndex = 1;
            ComputePathSegment();
        }
    }

    void ComputePathSegment() {
        var from = destinations[currentDestIndex - 1];
        var to = destinations[currentDestIndex];
        path = Pathfinding.FindPath(from, to);

        if (path == null || path.Count < 2) {
            Debug.LogError($"WaypointAgent: no path from {from.name} to {to.name}.", this);
            enabled = false;
            return;
        }

        pathIndex = 1;
        isMoving = true;
    }

    void FixedUpdate() {
        if (!isMoving) return;

        if (workerAnimator != null) {
            AnimatorStateInfo st = workerAnimator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("walk")) return;
        }

        Vector3 targetPos = path[pathIndex].transform.position;
        Vector3 dir = targetPos - rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) {
            Quaternion desiredRot = Quaternion.LookRotation(dir.normalized);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, desiredRot, rotationSpeed * Time.fixedDeltaTime));
        }

        Vector3 nextPos = Vector3.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime);
        rb.MovePosition(nextPos);

        if (Vector3.Distance(rb.position, targetPos) < 0.05f)
            AdvanceWaypoint();
    }

    void OnTriggerEnter(Collider other) {
        if (!isMoving) return;
        if (other.gameObject == path[pathIndex].gameObject)
            AdvanceWaypoint();
    }

    void AdvanceWaypoint() {
        Debug.Log($"Reached waypoint {pathIndex}: {path[pathIndex].name}", path[pathIndex]);
        pathIndex++;

        if (pathIndex >= path.Count) {
            Debug.Log($"Reached destination {currentDestIndex}: {destinations[currentDestIndex].name}", destinations[currentDestIndex]);
            currentDestIndex++;

            if (currentDestIndex < destinations.Count) {
                ComputePathSegment();
            }
            else if (canLoop) {
                Debug.Log("WaypointAgent: looping back to start.", this);
                var startWP = destinations[0];
                rb.position = new Vector3(startWP.transform.position.x, rb.position.y, startWP.transform.position.z);
                currentDestIndex = 1;
                ComputePathSegment();
            }
            else {
                isMoving = false;
                Debug.Log("WaypointAgent: completed all destinations.", this);
            }
        }
    }
}
