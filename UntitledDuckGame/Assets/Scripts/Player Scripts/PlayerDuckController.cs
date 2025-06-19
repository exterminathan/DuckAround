using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerDuckController : MonoBehaviour
{
    #region Variables
    [Header("Rig Settings")]
    [SerializeField] private Transform rigTarget;

    [SerializeField] private Transform root;
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform meshBase;

    [SerializeField] private float quackRotation = 30f;
    [SerializeField] private float quackDuration = 0.1f;


    [Header("Camera/Perspective Settings")]
    public Transform isoCamera;
    private Vector3 isoForward;
    private Vector3 isoRight;
    private Rigidbody rb;

    [Header("Game Settings")]
    [SerializeField] private bool isHoldingInMouth = false;
    [SerializeField] private bool isBrokenFree = false;

    [SerializeField] private LayerMask playerBlockingLayerMask;

    [Header("Movement Settings")]
    private bool canTraverse { get; set; } = true;
    private bool canFlex { get; set; } = true;

    [SerializeField] private float moveSpeed = 5f;
    [Range(0.5f, 1f)]
    [SerializeField] private float diagonalFactor = 1.85f; 

    [SerializeField] private float dampeningFactor = 0.05f;

    [SerializeField] private float verticalMinClamp, verticalMaxClamp, horizontalMinClamp, horizontalMaxClamp;

    private int keysPressed = 0;

    private float rig_drop_distance = -0.14f;

    #endregion

    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (isoCamera == null && Camera.main != null)
        {
            isoCamera = Camera.main.transform;
        }

        Vector3 f = isoCamera.forward;
        f.y = 0f;
        isoForward = f.normalized;
        isoRight = Vector3.Cross(Vector3.up, isoForward).normalized;

        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (isBrokenFree)
        {
            if (canFlex && canTraverse)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StartCoroutine(Quack());
                }

                float h = -Input.GetAxis("Horizontal");
                float v = -Input.GetAxis("Vertical");

                Vector3 input = new Vector3(h, 0f, v);

                if (input.sqrMagnitude > 0f)
                {
                    input = input.normalized;

                    float speedMultiplier = (Mathf.Abs(h) > 0 && Mathf.Abs(v) > 0) ? diagonalFactor : 1f;

                    Vector3 move = input * moveSpeed * speedMultiplier * Time.deltaTime;

                    transform.Translate(move, Space.World);
                }
            }
        }
        else
        {
            //do the stuff to break free, show tooltip, etc. 
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
            {
                keysPressed++;
                Debug.Log("Key pressed: " + keysPressed);
            }

            if (keysPressed > 15)
            {
                isBrokenFree = true;

                Debug.Log("You broke free!");

                //disable mesh base (break apart later)
                meshBase.gameObject.SetActive(false);
                //move position down by rig_drop_distance
                Debug.Log("Position before drop: " + transform.position);
                Vector3 newPos = new Vector3(transform.position.x, transform.position.y + rig_drop_distance, transform.position.z);
                transform.Translate(newPos, Space.World);
                Debug.Log("Position after drop: " + transform.position);

            }
        }
        
    }

    private IEnumerator Quack()
    {
        float duration = quackDuration / 2;
        Quaternion initialRot = mouth.localRotation;
        Quaternion openRot    = initialRot * Quaternion.Euler(0f, quackRotation, 0f);

        // ——— Open mouth ———
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            mouth.localRotation = Quaternion.Lerp(initialRot, openRot, t / duration);
            yield return null;
        }
        mouth.localRotation = openRot;

        // ——— Close mouth ———
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            mouth.localRotation = Quaternion.Lerp(openRot, initialRot, t / duration);
            yield return null;
        }
        mouth.localRotation = initialRot;

        Debug.Log("Quack!");
    }

}
