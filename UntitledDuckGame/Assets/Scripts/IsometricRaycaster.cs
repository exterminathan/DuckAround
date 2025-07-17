using UnityEngine;

public class IsometricRaycaster : MonoBehaviour {
    #region Public Variables
    [Header("Setup")]
    public Camera mainCamera;

    [Header("Layer Masks")]
    public LayerMask vertical_ik_layer;
    public LayerMask block_ik_layer;
    public LayerMask cursorIntersect_ik_layer;
    public LayerMask floor_ik_layer;

    public LayerMask combinedBlockerIgnoreLayer;


    [Header("IK Target")]
    [Tooltip("The IK target that controls the IK rig")]
    [SerializeField] private Transform ik_target;

    [Tooltip("The bone point used for cursor intersection raycasts")]
    public GameObject bone_point;

    [Tooltip("The pivot point, in this case the Armature")]
    public GameObject rotate_pivot;


    [Header("IK Interaction Objects")]
    public GameObject vertical_ik_plane;
    public GameObject floor_ik_plane;

    [Header("IK Colliders")]
    private Collider vertical_ik_collider;
    private Collider floor_ik_collider;
    private Collider blocker_ik_collider;


    private MeshRenderer vertical_ik_meshRenderer;


    [Header("Cursor Intersection Planes")]
    [SerializeField] private MeshRenderer primaryCursorIntersect;
    [SerializeField] private MeshRenderer upperCursorIntersect;
    [SerializeField] private MeshRenderer lowerCursorIntersect;

    [Header("Debug Options")]
    public bool showDev = false;
    private bool useVertical = true;
    private bool allowRotation = true;

    [Header("Random Variables")]
    [Tooltip("how far away from edge of vertical external influence affects")]
    public float outbound_range_follow_dist = .05f;


    // last recorded location of the IK target
    private Vector3 lastRecordedLocation = new Vector3(0, 0, 0);
    private bool wentOutOfVertical = false;
    #endregion

    #region Slerp Settings
    private Vector3 IKTargetStartPosition;
    private Vector3 IKTargetEndPosition;
    private float ikLerpTimer = 0f;
    private float ikLerpDuration = 0.05f;
    private bool isLerpingIK = false;
    #endregion


    private float frameCt;

    // --------------- UNITY METHODS --------------- //
    void Start() {
        // assign main camera for raycasts
        if (mainCamera == null) mainCamera = Camera.main;

        // Get colliders and Mesh Renderers for collider ranges
        if (vertical_ik_plane != null) {
            vertical_ik_collider = vertical_ik_plane.GetComponent<Collider>();
            vertical_ik_meshRenderer = vertical_ik_plane.GetComponent<MeshRenderer>();
        }
        if (floor_ik_plane != null)
            floor_ik_collider = floor_ik_plane.GetComponent<Collider>();

    }


    void Update() {
        //Determine visibility of debug elements
        vertical_ik_meshRenderer.enabled = showDev;
        primaryCursorIntersect.enabled = showDev;
        upperCursorIntersect.enabled = showDev;
        lowerCursorIntersect.enabled = showDev;

        //enable/disable cursor intersection planes based on armature rotation limits
        if (rotate_pivot.transform.rotation.eulerAngles.y < 280f && rotate_pivot.transform.rotation.eulerAngles.y > 175f) {
            //Enables the lower cursor intersection plane when at rotation limit
            lowerCursorIntersect.gameObject.SetActive(true);
            upperCursorIntersect.gameObject.SetActive(false);

        }
        else {
            //Disables the lower cursor intersection plane when not at rotation limit
            lowerCursorIntersect.gameObject.SetActive(false);
            upperCursorIntersect.gameObject.SetActive(true);

        }

        //Lerp tracker for any IK target movement 
        if (isLerpingIK) {
            ikLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(ikLerpTimer / ikLerpDuration);
            ik_target.transform.position = Vector3.Lerp(IKTargetStartPosition, IKTargetEndPosition, t);

            if (t >= 1f) {
                isLerpingIK = false;
            }
        }


        //ray from camera to mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // LOGIC BLOCKS //
        // Check 1: Blocker IK Range (Any scene Objects)
        if (Physics.Raycast(ray, out RaycastHit hit0, Mathf.Infinity, block_ik_layer)) {

            if (!wentOutOfVertical) {
                wentOutOfVertical = true;
                //Debug.Log("Went out of vertical IK plane");
            }

            //if a sphere cast from hit point hits vertical ik collider, then find closest point on blocker collider
            // and move ik target to that point

            //Debug.Log("On blocker");
            // if IK target inside any blocker
            //draw debug check sphere
            if (Physics.CheckSphere(ik_target.position, 0.1f, combinedBlockerIgnoreLayer)) {
                //find closest point on blocker collider
                // move ik target to that point
                Collider[] blockers = Physics.OverlapSphere(ik_target.position, 0.05f, block_ik_layer);

                if (blockers.Length > 0) {
                    Vector3 closestPoint = blockers[0].ClosestPoint(ik_target.position);
                    StartIKLerp(closestPoint);
                }
            }




            //Debug ray and sphere
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.yellow, 0.5f);
            if (showDev) ShowDebugSphere(hit0.point, Color.yellow);

            // find intersection point between blocker and vertical IK plane, and use that hit point
            // as new ik target position
            // (so when you are on top of a blocker, the IK target will move to the blocker surface)
            // rotate
            Vector3 flatDirection = hit0.point - rotate_pivot.transform.position;
            flatDirection.y = 0f;

            if (flatDirection != Vector3.zero) {
                Quaternion targetLook = Quaternion.LookRotation(flatDirection);

                float targetY = targetLook.eulerAngles.y + 90f;

                // normalize angle
                if (targetY > 180f) targetY -= 360f;

                // only rotate outside of range
                if (targetY > -90f || targetY < -180f) {
                    Quaternion targetRotation = Quaternion.Euler(-90f, targetY, 0f);

                    if (allowRotation) {
                        rotate_pivot.transform.rotation = Quaternion.Slerp(
                            rotate_pivot.transform.rotation,
                            targetRotation,
                            Time.deltaTime * 20f
                        );
                    }
                }
            }


        }

        // Check 2: Vertical IK Range (Random White Gibberish in Editor)
        else if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, vertical_ik_layer)) {
            //if hit vertical collider, determine new position of IK target
            if (hit.collider == vertical_ik_collider && useVertical) {
                //Mark last recorded hit location
                lastRecordedLocation = hit.point;

                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.blue, 0.5f);

                // Show debug sphere if dev enabled
                if (showDev) ShowDebugSphere(hit.point, Color.blue);

                // Double-check if the hit point is colliding with any blockers 
                // (might have to move this out of here if blocker check happens before this one)

                // check if IK target returned to the vertical IK range
                if (wentOutOfVertical) {
                    //Debug.Log("Went back in to vert range");
                    StartIKLerp(hit.point);
                    wentOutOfVertical = false;
                }
                else {
                    StartIKLerp(hit.point);
                }

            }
        }

        //Check 3: Cursor Intersection Plane (Purple in Editor) 
        else if (Physics.Raycast(ray, out RaycastHit hit3, Mathf.Infinity, cursorIntersect_ik_layer)) {
            //if hit intersection plane, determine start position and direction
            Vector3 start_pos = hit3.point;
            Vector3 start_direction = (bone_point.transform.position - start_pos).normalized;

            if (showDev) ShowDebugSphere(start_pos, Color.cyan);

            // raycast #2: from intersection plane hit point towards bone root
            if (Physics.Raycast(start_pos, start_direction, out RaycastHit hit4, Mathf.Infinity, vertical_ik_layer)) {
                Debug.DrawRay(start_pos, start_direction, Color.cyan, .1f);

                // find a point within the Vertical IK range to move the IK target to 
                if (vertical_ik_collider.bounds.Contains(hit4.point)) {
                    //slight push towards inside of range for cleaner collision
                    StartIKLerp(hit4.point + start_direction * outbound_range_follow_dist);

                }
            }
        }

        //Check 4: Floor IK Range - Rotation (The floor)
        else if (Physics.Raycast(ray, out RaycastHit hit2, Mathf.Infinity, floor_ik_layer)) {
            //if hit specific floor collider, determine new rotation of armature
            if (hit2.collider == floor_ik_collider) {
                if (!wentOutOfVertical) {
                    wentOutOfVertical = true;
                    //Debug.Log("Went out of vertical IK plane");
                }

                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 0.5f);
                //Debug.Log($"Hit horizontal ik range at {hit.point}");

                if (showDev) ShowDebugSphere(hit2.point, Color.red);

                //hit point is on ground
                //get armature position and rotation
                Vector3 flatDirection = hit2.point - rotate_pivot.transform.position;
                flatDirection.y = 0f;

                // use LookRotation to find where to point, and rotate
                if (flatDirection != Vector3.zero) {
                    Quaternion targetLook = Quaternion.LookRotation(flatDirection);

                    float targetY = targetLook.eulerAngles.y + 90f;

                    // normalize angle
                    if (targetY > 180f) targetY -= 360f;

                    // only rotate outside of range
                    if (targetY > -90f || targetY < -180f) {
                        Quaternion targetRotation = Quaternion.Euler(-90f, targetY, 0f);

                        if (allowRotation) {
                            rotate_pivot.transform.rotation = Quaternion.Slerp(
                                rotate_pivot.transform.rotation,
                                targetRotation,
                                Time.deltaTime * 20f
                            );
                        }
                    }
                }
            }
        }

    }



    // --------------- HELPER METHODS -------------- //
    // helper function to lerp the ik target to a new position smoothly
    void StartIKLerp(Vector3 targetPosition) {
        IKTargetStartPosition = ik_target.transform.position;
        IKTargetEndPosition = targetPosition;
        ikLerpTimer = 0f;
        isLerpingIK = true;
    }



    // --------------- DEBUG METHODS --------------- //
    // debug helper function to create a sphere at agiven position with a specified color
    void ShowDebugSphere(Vector3 position, Color debugColor) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, 1f);
    }
}