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

    }


    void Update() {


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