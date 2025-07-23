using UnityEngine;
using UnityEngine.AI;
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
    [SerializeField] private Transform ik_target;
    public GameObject bone_point;
    public GameObject rotate_pivot;


    [Header("Rotation Parameters")]
    public float innerZoneRangeX = 225f;
    public float minPivotAngle = -90f;
    public float maxPivotAngle = 90f;
    public float rotationSmoothSpeed = 5f;

    [Header("Horizontal IK Parameters")]
    public float minIKX = 0f;
    public float maxIKX = 0f;
    // how much scrolling moves the ik target 
    public float scrollIncrement = 0.1f;

    [Header("Vertical IK Parameters")]
    public float innerZoneRangeY = 200f;
    public float minIKY = 0.05f;
    public float maxIKY = 2.562f;

    [Header("Boundary Visuals")]
    public Canvas uiCanvas;
    public Color boundaryColor = Color.white;
    public float fadeDistance = 50f;

    [Header("Debug Options")]
    public bool showDev = false;

    #endregion

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
        HandleRotation();
        HandleVerticalIK();
        HandleHorizontalIK();
        UpdateBoundaryFade();
    }

    private void HandleRotation() {
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRangeX;
        float maxX = centerX + innerZoneRangeX;
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
    private void HandleHorizontalIK() {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f) {
            var local = ik_target.localPosition;
            float targetX = Mathf.Clamp(local.x - scroll * scrollIncrement, minIKX, maxIKX);
            local.x = Mathf.Lerp(local.x, targetX, 1f / 7f);
            ik_target.localPosition = local;
        }
    }

    #region Debug
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
        float minX = centerX - innerZoneRangeX;
        float maxX = centerX + innerZoneRangeX;
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

    void ShowDebugSphere(Vector3 position, Color debugColor) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, 1f);
    }
    #endregion
}