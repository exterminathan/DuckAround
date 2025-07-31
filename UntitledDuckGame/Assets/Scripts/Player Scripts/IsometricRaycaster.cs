// IsometricRaycaster.cs
using System.Linq;
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

    [Header("Collision Settings")]
    [Tooltip("Layers that block arm rotation")]
    public LayerMask rotationBlockingLayerMask;
    #endregion

    private Image leftBoundaryImage;
    private Image rightBoundaryImage;
    private Image topBoundaryImage;
    private Image bottomBoundaryImage;

    public GameObject[] armObjects;
    //assigned at runtime
    private Collider[] armColliders;
    public ArmHitForwarder[] armPushers { get; private set; }


    void Start() {
        if (mainCamera == null) mainCamera = Camera.main;
        if (uiCanvas == null) uiCanvas = FindFirstObjectByType<Canvas>();

        armColliders = armObjects
            .SelectMany(o => o.GetComponentsInChildren<Collider>())
            .ToArray();

        armPushers = armObjects
            .SelectMany(o => o.GetComponentsInChildren<ArmHitForwarder>())
            .ToArray();
    }

    void Update() {
        HandleRotation();
        HandleVerticalIK();
        HandleHorizontalIK();
    }

    void LateUpdate() {
        //update arm pusher velocity/position
        foreach (var p in armPushers) {
            Vector3 currentPos = p.transform.position;
            Vector3 delta = currentPos - p.lastPos;

            p.velocity = delta / Time.deltaTime;
            p.lastPos = currentPos;
        }
    }

    private void HandleRotation() {
        float centerX = Screen.width * 0.5f;
        float minX = centerX - innerZoneRangeX;
        float maxX = centerX + innerZoneRangeX;
        float t = Mathf.Clamp01((Input.mousePosition.x - minX) / (maxX - minX));

        float targetAngle = Mathf.Lerp(maxPivotAngle, minPivotAngle, t);
        var pivot = rotate_pivot.transform;
        float currY = pivot.localEulerAngles.y;
        float smoothedY = Mathf.LerpAngle(currY, targetAngle, Time.deltaTime * rotationSmoothSpeed);
        float rawDelta = Mathf.DeltaAngle(currY, smoothedY);
        float allowedDelta = rawDelta;

        // apply the clamped rotation
        var eul = pivot.localEulerAngles;
        eul.y = currY + allowedDelta;


        // sweep checks for arm colliders position after rotation about pivot
        foreach (var c in armColliders) {
            if (!(c is BoxCollider box)) continue;

            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
            // box center after pivot rotation
            Vector3 worldOffset = box.transform.position - pivot.position;
            Vector3 rotatedCenter = pivot.position
                                  + Quaternion.Euler(0f, rawDelta, 0f)
                                    * worldOffset;
            // final orientation
            Quaternion rotatedOri = box.transform.rotation
                                  * Quaternion.Euler(0f, rawDelta, 0f);

            // clamp if overlapped
            Collider[] hits = Physics.OverlapBox(
                rotatedCenter,
                halfExtents,
                rotatedOri,
                rotationBlockingLayerMask,
                QueryTriggerInteraction.Ignore
            );
            if (hits.Length > 0) {
                // figure out safe rotation angle
                float low = 0f, high = rawDelta;
                for (int i = 0; i < 4; i++) {
                    float mid = (low + high) * 0.5f;
                    Vector3 midCenter = pivot.position
                                      + Quaternion.Euler(0f, mid, 0f)
                                        * worldOffset;
                    Quaternion midOri = box.transform.rotation
                                      * Quaternion.Euler(0f, mid, 0f);

                    if (Physics.OverlapBox(midCenter, halfExtents, midOri,
                            rotationBlockingLayerMask,
                            QueryTriggerInteraction.Ignore).Length == 0) {
                        low = mid;
                    }
                    else {
                        high = mid;
                    }
                }
                allowedDelta = Mathf.Min(allowedDelta, low);
            }
        }

        // finalized rotation
        var e = pivot.localEulerAngles;
        e.y = currY + allowedDelta;
        pivot.localEulerAngles = e;
    }

    private void HandleVerticalIK() {
        float centerY = Screen.height * 0.5f;
        float minY = centerY - innerZoneRangeY;
        float maxY = centerY + innerZoneRangeY;
        float t = Mathf.Clamp01((Input.mousePosition.y - minY) / (maxY - minY));

        float targetY = Mathf.Lerp(minIKY, maxIKY, t);
        Vector3 pos = ik_target.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * rotationSmoothSpeed);
        ik_target.position = pos;
    }

    private void HandleHorizontalIK() {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f) {
            Vector3 local = ik_target.localPosition;
            float targetX = Mathf.Clamp(local.x - scroll * scrollIncrement, minIKX, maxIKX);
            local.x = Mathf.Lerp(local.x, targetX, 1f / 7f);
            ik_target.localPosition = local;
        }
    }

    #region Debug
    void CreateBoundaryLines() {
        System.Func<string, Image> makeLine = name => {
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
        if (leftBoundaryImage == null) return;
        float cX = Screen.width * 0.5f, cY = Screen.height * 0.5f;
        float minX = cX - innerZoneRangeX, maxX = cX + innerZoneRangeX;
        float minY = cY - innerZoneRangeY, maxY = cY + innerZoneRangeY;
        float mX = Input.mousePosition.x, mY = Input.mousePosition.y;

        var leftRT = leftBoundaryImage.rectTransform;
        var rightRT = rightBoundaryImage.rectTransform;
        var topRT = topBoundaryImage.rectTransform;
        var bottomRT = bottomBoundaryImage.rectTransform;

        leftRT.anchoredPosition = new Vector2(minX - cX, 0f);
        rightRT.anchoredPosition = new Vector2(maxX - cX, 0f);
        topRT.anchoredPosition = new Vector2(0f, maxY - cY);
        bottomRT.anchoredPosition = new Vector2(0f, minY - cY);

        leftRT.sizeDelta = new Vector2(2f, Screen.height);
        rightRT.sizeDelta = new Vector2(2f, Screen.height);
        topRT.sizeDelta = new Vector2(Screen.width, 2f);
        bottomRT.sizeDelta = new Vector2(Screen.width, 2f);

        float alphaL = (mX <= minX + fadeDistance) ? Mathf.Clamp01(1f - ((mX - minX) / fadeDistance)) : 0f;
        float alphaR = (mX >= maxX - fadeDistance) ? Mathf.Clamp01(1f - ((maxX - mX) / fadeDistance)) : 0f;
        float alphaT = (mY >= maxY - fadeDistance) ? Mathf.Clamp01(1f - ((maxY - mY) / fadeDistance)) : 0f;
        float alphaB = (mY <= minY + fadeDistance) ? Mathf.Clamp01(1f - ((mY - minY) / fadeDistance)) : 0f;

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
