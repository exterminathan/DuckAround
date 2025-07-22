using UnityEngine;
using UnityEngine.UI;

// during click on interactable like alarm
// save current rotation
// rotate to face it, do interaction
// reset rotation to before after click released



public class IsometricRaycaster : MonoBehaviour {
    #region Public Variables
    [Header("Setup")]
    public Camera mainCamera;

    [Header("IK Target")]
    [Tooltip("The IK target that controls the IK rig")]
    [SerializeField] private Transform ik_target;
    [Tooltip("The bone point used for cursor intersection raycasts")]
    public GameObject bone_point;
    [Tooltip("The pivot point, in this case the Armature")]
    public GameObject rotate_pivot;

    [Header("Pivot Rotation")]
    [Tooltip("Pivot’s angle at left edge (in degrees)")]
    public float minPivotAngle = -90f;
    [Tooltip("Pivot’s angle at right edge (in degrees)")]
    public float maxPivotAngle = 90f;
    [Tooltip("How fast the pivot lerps to target (higher = snappier)")]
    public float rotationSmoothSpeed = 5f;

    [Header("Screen Detection")]
    [Tooltip("Half-width in pixels of inner zone")]
    public float innerZoneRange = 100f;
    [Tooltip("Half-height in pixels of inner vertical zone")]
    public float innerZoneRangeY = 100f;

    [Header("Vertical IK Bounds")]
    [Tooltip("IK target Y at bottom edge")]
    public float minIKY = 0f;
    [Tooltip("IK target Y at top edge")]
    public float maxIKY = 2f;

    [Header("Boundary Visuals")]
    public Canvas uiCanvas;
    public Color boundaryColor = Color.white;
    public float fadeDistance = 50f;

    [Header("Debug Options")]
    public bool showDev = false;
    private bool useVertical = true;
    private bool allowRotation = true;

    [Header("Random Variables")]
    [Tooltip("how far away from edge of vertical external influence affects")]
    public float outbound_range_follow_dist = .05f;

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

    private Image leftBoundaryImage;
    private Image rightBoundaryImage;
    private Image topBoundaryImage;
    private Image bottomBoundaryImage;

    void Start() {
        if (mainCamera == null) mainCamera = Camera.main;
        if (uiCanvas == null) uiCanvas = FindFirstObjectByType<Canvas>();
        CreateBoundaryLines();
    }

    void Update() {
        HandleInnerZoneDetection();
        HandleVerticalIK();
        UpdateBoundaryFade();
    }

    private void HandleInnerZoneDetection() {
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRange;
        float maxX = centerX + innerZoneRange;
        float mouseX = Input.mousePosition.x;
        float t = Mathf.Clamp01((mouseX - minX) / (maxX - minX));

        float targetAngle = Mathf.Lerp(maxPivotAngle, minPivotAngle, t);
        float currY = rotate_pivot.transform.localEulerAngles.y;
        float smoothedY = Mathf.LerpAngle(currY, targetAngle, Time.deltaTime * rotationSmoothSpeed);
        var eul = rotate_pivot.transform.localEulerAngles;
        eul.y = smoothedY;
        rotate_pivot.transform.localEulerAngles = eul;
    }

    private void HandleVerticalIK() {
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float mouseY = Input.mousePosition.y;
        float t = Mathf.Clamp01((mouseY - minY) / (maxY - minY));

        float targetY = Mathf.Lerp(minIKY, maxIKY, t);
        var pos = ik_target.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * rotationSmoothSpeed);
        ik_target.position = pos;
    }

    void CreateBoundaryLines() {
        System.Func<string, Image> makeLine = (string name) => {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(uiCanvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, 0f);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        };
        leftBoundaryImage = makeLine("LeftInnerZoneLine");
        rightBoundaryImage = makeLine("RightInnerZoneLine");
        topBoundaryImage = makeLine("TopInnerZoneLine");
        bottomBoundaryImage = makeLine("BottomInnerZoneLine");
    }

    void UpdateBoundaryFade() {
        if (leftBoundaryImage == null || rightBoundaryImage == null) return;
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRange;
        float maxX = centerX + innerZoneRange;
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float mouseX = Input.mousePosition.x;
        float mouseY = Input.mousePosition.y;

        var leftRT = leftBoundaryImage.rectTransform;
        var rightRT = rightBoundaryImage.rectTransform;
        var topRT = topBoundaryImage.rectTransform;
        var bottomRT = bottomBoundaryImage.rectTransform;

        leftRT.anchoredPosition = new Vector2(minX - centerX, 0f);
        rightRT.anchoredPosition = new Vector2(maxX - centerX, 0f);
        topRT.anchoredPosition = new Vector2(0f, maxY - centerY);
        bottomRT.anchoredPosition = new Vector2(0f, minY - centerY);

        leftRT.sizeDelta = new Vector2(2f, Screen.height);
        rightRT.sizeDelta = new Vector2(2f, Screen.height);
        topRT.sizeDelta = new Vector2(Screen.width, 2f);
        bottomRT.sizeDelta = new Vector2(Screen.width, 2f);

        float alphaL = 0f;
        if (mouseX <= minX + fadeDistance) alphaL = Mathf.Clamp01(1f - ((mouseX - minX) / fadeDistance));
        float alphaR = 0f;
        if (mouseX >= maxX - fadeDistance) alphaR = Mathf.Clamp01(1f - ((maxX - mouseX) / fadeDistance));
        float alphaT = 0f;
        if (mouseY >= maxY - fadeDistance) alphaT = Mathf.Clamp01(1f - ((maxY - mouseY) / fadeDistance));
        float alphaB = 0f;
        if (mouseY <= minY + fadeDistance) alphaB = Mathf.Clamp01(1f - ((mouseY - minY) / fadeDistance));

        leftBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaL);
        rightBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaR);
        topBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaT);
        bottomBoundaryImage.color = new Color(boundaryColor.r, boundaryColor.g, boundaryColor.b, alphaB);
    }

    void StartIKLerp(Vector3 targetPosition) {
        IKTargetStartPosition = ik_target.transform.position;
        IKTargetEndPosition = targetPosition;
        ikLerpTimer = 0f;
        isLerpingIK = true;
    }

    void ShowDebugSphere(Vector3 position, Color debugColor) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, 1f);
    }
}
