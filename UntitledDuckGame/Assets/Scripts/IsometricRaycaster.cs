using UnityEngine;

public class IsometricRaycaster : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Your isometric (orthographic) camera")]
    public Camera isoCamera;
    [Tooltip("LayerMask for IK Range Colliders")]
    public LayerMask ik_layer;
    [Tooltip("Assign the ik range GameObjects here")]
    public GameObject horizontal_ik_range;
    public GameObject vertical_ik_range;

    private Collider horizontal_ik_collider;
    private Collider vertical_ik_collider;

    private Collider blocker_ik_collider;

    [SerializeField] private Transform ik_target;

    public bool showDev = false;

    void Start()
    {

        Cursor.visible = false;

        if (isoCamera == null) isoCamera = Camera.main;
        if (horizontal_ik_range != null)
            horizontal_ik_collider = horizontal_ik_range.GetComponent<Collider>();
        if (vertical_ik_range != null)
            vertical_ik_collider = vertical_ik_range.GetComponent<Collider>();
        

    }

    void Update()
    {

        //First ray check for vertical ik range
        Ray ray = isoCamera.ScreenPointToRay(Input.mousePosition);
        //blue debug line for vertical
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ik_layer))
        {
            if (hit.collider == vertical_ik_collider)
            {
                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.blue, 0.5f);

                Debug.Log($"Hit vertical ik range at {hit.point}");

                if (showDev) ShowDebugSphere(hit.point, Color.blue);

                ik_target.transform.position = hit.point;
            }
            else if (hit.collider == horizontal_ik_collider)
            {
                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 0.5f);
                Debug.Log($"Hit horizontal ik range at {hit.point}");

                if (showDev) ShowDebugSphere(hit.point, Color.red);

                //TODO rotate?
            }
            else if (hit.collider == blocker_ik_collider)
            {
                //in this case ignore. also this should take precedence over horizontal ik range
            }
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
}