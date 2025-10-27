using UnityEngine;

public enum StateName {
    IDLE,
    PATROL,
    ALERT,
    CHASING,
    RAGDOLL
}
[System.Serializable]
public struct ColorState {
    public StateName StateName;
    public Color StateColor;

}


public class WorkerVisualController : MonoBehaviour {
    [Header("Visual Components")]
    [SerializeField] private Renderer innerCircle;
    [SerializeField] private Renderer outerCircle;
    [SerializeField] private float lerpSpeed = 2f;

    [Header("Opacity")]
    [SerializeField] private float innerOpacity = 0.098f;
    [SerializeField] private float outerOpacity = 1f;
    [SerializeField] private float colorlerpSpeed = 4f;

    [Header("Color States")]
    [SerializeField] private ColorState[] colorStates;

    #region Private Fields
    private Material innerMat;
    private Material outerMat;

    private float currentAngle;
    private float currentDistance;
    private Color currentInnerColor;
    private Color currentOuterColor;


    private float targetAngle;
    private float targetDistance;
    private Color targetInnerColor;
    private Color targetOuterColor;

    private bool isParamLerping = false;
    private bool isColorLerping = false;
    #endregion

    void Awake() {
        innerMat = innerCircle.material;
        outerMat = outerCircle.material;

        currentInnerColor = innerMat.GetColor("_Color");
        currentOuterColor = outerMat.GetColor("_Color");
        Debug.Log($"[WorkerVisualController] Awake called. Initial colors: Inner: {currentInnerColor}, Outer: {currentOuterColor}");

    }

    public void SetVisualParameters(float angle, float distance) {
        Debug.Log($"[WorkerVisualController] SetVisualParameters called with angle: {angle}, distance: {distance}");
        targetAngle = angle;
        targetDistance = distance;
        isParamLerping = true;
    }

    public void SetVisualColor(StateName state, bool opacityOff = false) {
        Debug.Log($"[WorkerVisualController] SetVisualColor called with state: {state}");

        Color newColor = GetColorFromState(state);
        
        if (opacityOff) {
            targetInnerColor = new Color(newColor.r, newColor.g, newColor.b, 0f);
            targetOuterColor = new Color(newColor.r, newColor.g, newColor.b, 0f);
            isColorLerping = true;
            return;
        }

        targetInnerColor = new Color(newColor.r, newColor.g, newColor.b, innerOpacity);
        targetOuterColor = new Color(newColor.r, newColor.g, newColor.b, outerOpacity);
        isColorLerping = true;
    }

    void Update() {
        if (isColorLerping) {
            //color lerp
            currentInnerColor = Color.Lerp(currentInnerColor, targetInnerColor, Time.deltaTime * colorlerpSpeed);
            currentOuterColor = Color.Lerp(currentOuterColor, targetOuterColor, Time.deltaTime * colorlerpSpeed);
            innerMat.SetColor("_Color", currentInnerColor);
            outerMat.SetColor("_Color", currentOuterColor);

        }

        if (isParamLerping) {
            //param lerp
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * lerpSpeed);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * lerpSpeed);

            if (Mathf.Abs(currentAngle - targetAngle) < 0.01f && Mathf.Abs(currentDistance - targetDistance) < 0.01f) {
                isParamLerping = false;
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

    // private helper to get color from state
    private Color GetColorFromState(StateName state) {
        foreach (var cs in colorStates) {
            if (cs.StateName == state) {
                return cs.StateColor;
            }
        }
        return Color.white;
    }

}
