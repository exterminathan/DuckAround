using TMPro;
using UnityEngine;

public class IsometricRaycaster : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Your isometric (orthographic) camera")]
    public Camera isoCamera;
    [Tooltip("LayerMask for IK Range Colliders")]
    public LayerMask ik_layer;

    [Tooltip("Armature (or whatever controls player rotation)")]
    public GameObject rotate_pivot;

    [Tooltip("how far away from edge of vertical external influence affects")]
    public float outbound_range_follow_dist = .05f;

    [Tooltip("Assign the ik range GameObjects here")]
    public GameObject horizontal_ik_range;
    public GameObject vertical_ik_range;

    public GameObject floor_ik_range;


    public GameObject bone_point;

    public LayerMask block_ik;
    public LayerMask intersectionPlane_ik_layer;
    private Collider vertical_ik_collider;

    private Collider floor_ik_collider;

    private Collider blocker_ik_collider;

    [SerializeField] private Transform ik_target;


    public bool showDev = false;
    public bool useVertical = false;
    public bool allowRotation = true;

    private Vector3 lastRecordedLocation = new Vector3(0, 0, 0);
    private bool wentOutOfVertical = false;

    private MeshRenderer vert_range_meshRenderer;
    [SerializeField] private MeshRenderer primaryCursorIntersect;
    [SerializeField] private MeshRenderer upperCursorIntersect;
    [SerializeField] private MeshRenderer lowerCursorIntersect;


    #region Slerp Settings
    private Vector3 IKTargetStartPosition;
    private Vector3 IKTargetEndPosition;
    private float ikLerpTimer = 0f;
    private float ikLerpDuration = 0.2f;
    private bool isLerpingIK = false;
    #endregion

    private float frameCt;

    void Start()
    {

        // if (showDev)
        //     Cursor.visible = true;
        // else
        //     Cursor.visible = false;

        if (isoCamera == null) isoCamera = Camera.main;

        if (vertical_ik_range != null)
        {
            vertical_ik_collider = vertical_ik_range.GetComponent<Collider>();
            vert_range_meshRenderer = vertical_ik_range.GetComponent<MeshRenderer>();
            if (showDev) vert_range_meshRenderer.enabled = true;
        }
        if (floor_ik_range != null)
            floor_ik_collider = floor_ik_range.GetComponent<Collider>();

    }

    void Update()
    {
        frameCt++;


        vert_range_meshRenderer.enabled = showDev;
        primaryCursorIntersect.enabled = showDev;
        upperCursorIntersect.enabled = showDev;
        lowerCursorIntersect.enabled = showDev;

        // log rotate pivot rotation every 5 frames
        if (frameCt % 5 == 0)
        {
            Debug.Log($"Rotate pivot rotation: {rotate_pivot.transform.rotation.eulerAngles.y}");
        }

        //enable/disable cursor intersect meshes based on armature rotation
        if (rotate_pivot.transform.rotation.eulerAngles.y < 280f && rotate_pivot.transform.rotation.eulerAngles.y > 180f)
        {
            Debug.Log("Rotate pivot is in the range of 180 to 270 degrees");
            lowerCursorIntersect.gameObject.SetActive(true);
            upperCursorIntersect.gameObject.SetActive(false);

        }
        else
        {
            lowerCursorIntersect.gameObject.SetActive(false);
            upperCursorIntersect.gameObject.SetActive(true);

        }

        // lerp tracker
        if (isLerpingIK)
        {
            ikLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(ikLerpTimer / ikLerpDuration);
            ik_target.transform.position = Vector3.Lerp(IKTargetStartPosition, IKTargetEndPosition, t);

            if (t >= 1f)
            {
                isLerpingIK = false;
            }
        }

        //First ray check for vertical ik range
        Ray ray = isoCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ik_layer))
        {
            //if vertical collider - ik movement
            if (hit.collider == vertical_ik_collider && useVertical)
            {
                lastRecordedLocation = hit.point;
                //Debug.DrawRay(ray.origin, ray.direction * 100f, Color.blue, 0.5f);

                //Debug.Log($"Hit vertical ik range at {hit.point}");

                if (showDev) ShowDebugSphere(hit.point, Color.blue);

                if (!Physics.CheckSphere(hit.point, 0.05f, block_ik))
                {
                    // check if just came back in to vert range to lerp movement smooth
                    if (wentOutOfVertical)
                    {
                        Debug.Log("Went back in to vert range");
                        StartIKLerp(hit.point);
                        wentOutOfVertical = false;
                    }
                    else
                    {
                        ik_target.transform.position = hit.point;
                    }
                }
            }
            // first, check raycast against intersection plane
            // then raycast towards root bone position and check collision
            // with vertical_range and move ik_target to hit pt. inside of range
            else if (Physics.Raycast(ray, out RaycastHit hit2, Mathf.Infinity, intersectionPlane_ik_layer))
            {
                //if hit intersection plane
                Vector3 start_pos = hit2.point;
                Vector3 start_direction = (bone_point.transform.position - start_pos).normalized;

                if (Physics.Raycast(start_pos, start_direction, out RaycastHit hit3, Mathf.Infinity, ik_layer))
                {
                    Debug.DrawRay(start_pos, start_direction, Color.cyan, .1f);
                    if (vertical_ik_range.GetComponent<Collider>().bounds.Contains(hit3.point))
                    {
                        //slight push towards inside of range for cleaner collision
                        StartIKLerp(hit3.point + start_direction * outbound_range_follow_dist);

                    }
                }
            }
            //if horizontal collider - rotate
            else if (hit.collider == floor_ik_collider)
            {
                if (!wentOutOfVertical)
                {
                    wentOutOfVertical = true;
                    Debug.Log("Went out of vert range");
                }
                //Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 0.5f);
                //Debug.Log($"Hit horizontal ik range at {hit.point}");

                if (showDev) ShowDebugSphere(hit.point, Color.red);

                //hit point is on ground
                //get armature position and rotation
                Vector3 flatDirection = hit.point - rotate_pivot.transform.position;
                flatDirection.y = 0f;

                // use LookRotation to find where to point, and rotate
                // 
                if (flatDirection != Vector3.zero)
                {
                    Quaternion targetLook = Quaternion.LookRotation(flatDirection);

                    float targetY = targetLook.eulerAngles.y + 90f;

                    // normalize angle
                    if (targetY > 180f) targetY -= 360f;

                    // only rotate outside of range
                    if (targetY > -90f || targetY < -180f)
                    {
                        Quaternion targetRotation = Quaternion.Euler(-90f, targetY, 0f);

                        if (allowRotation)
                        {
                            rotate_pivot.transform.rotation = Quaternion.Slerp(
                                rotate_pivot.transform.rotation,
                                targetRotation,
                                Time.deltaTime * 5f
                            );
                        }
                    }
                }
            } // else if (blockers like scene objects)
        }
    }

    void ShowDebugSphere(Vector3 position, Color debugColor)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TMP_DEBUG_SPHERE";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .1f;
        sphere.GetComponent<Renderer>().material.color = debugColor;
        Destroy(sphere, 1f);
    }

    void StartIKLerp(Vector3 targetPosition)
    {
        IKTargetStartPosition = ik_target.transform.position;
        IKTargetEndPosition = targetPosition;
        ikLerpTimer = 0f;
        isLerpingIK = true;
    }
    



}