using System;
using Unity.VisualScripting;
using UnityEngine;

public class WorkerVisualController : MonoBehaviour
{
    [SerializeField] private Renderer innerCircle;
    [SerializeField] private Renderer outerCircle;
    [SerializeField] private float lerpSpeed = 2f;

    private Material innerMat;
    private Material outerMat;

    private float currentAngle;
    private float currentDistance;

    private float targetAngle;
    private float targetDistance;

    private bool isLerping = false;

    void Awake() {
        innerMat = innerCircle.material;
        outerMat = outerCircle.material;

    }

    public void SetVisualParameters(float angle, float distance) {
        Debug.Log($"[WorkerVisualController] SetVisualParameters called with angle: {angle}, distance: {distance}");
        targetAngle = angle;
        targetDistance = distance;
        isLerping = true;
    }

    void Update() {
        if (!isLerping) return;

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * lerpSpeed);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * lerpSpeed);

        if (Mathf.Abs(currentAngle - targetAngle) < 0.01f && Mathf.Abs(currentDistance - targetDistance) < 0.01f) {
            isLerping = false;
        }

        var arc1 = 180 - currentAngle / 2f;
        var arc2 = 180 + currentAngle / 2f;

        innerMat.SetFloat("_Angle", currentAngle);
        innerMat.SetFloat("_Arc1", arc1);
        innerMat.SetFloat("_Arc2", arc2);

        outerMat.SetFloat("_Angle", currentAngle);
        outerMat.SetFloat("_Arc1", arc1);
        outerMat.SetFloat("_Arc2", arc2);
    }
    
    

    
}
