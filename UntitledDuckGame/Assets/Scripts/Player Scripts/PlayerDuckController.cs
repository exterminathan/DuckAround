using System.Collections;
using AmplifyShaderEditor;
using UnityEngine;

public class PlayerDuckController : MonoBehaviour
{
    #region Variables
    [Header("Rig Settings")]
    [SerializeField] private Transform rigTarget;

    [SerializeField] private Transform root;
    [SerializeField] private Transform mouth;

    [SerializeField] private float quackRotation = 30f;
    [SerializeField] private float quackDuration = 0.1f;



    [Header("Movement Settings")]
    private bool canTraverse { get; set; } = true;
    private bool canFlex { get; set; } = true;

    [SerializeField] private float moveSpeed = 5f;

    [SerializeField] private float dampeningFactor = 0.05f;

    [SerializeField] private float verticalMinClamp, verticalMaxClamp, horizontalMinClamp, horizontalMaxClamp;
    #endregion

    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    void Update()
    {
        if (canFlex && canTraverse)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                StartCoroutine(Quack());
            }

            Vector3 input = new Vector3(
                            -Input.GetAxisRaw("Horizontal"),
                            0f,
                            -Input.GetAxisRaw("Vertical")).normalized;

            if (input.sqrMagnitude > 0f)
            {
                transform.Translate(input * moveSpeed * Time.deltaTime, Space.World);
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
