using System.Security.Cryptography;
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
    // used for scaling vis for distance
    private RectTransform innerRectTransform;
    [SerializeField] private Renderer outerCircle;
    [SerializeField] private float paramLerpSpeed = 2f;

    [Header("Opacity")]
    [SerializeField] private float innerOpacity = 0.098f;
    [SerializeField] private float outerOpacity = 1f;
    [SerializeField] private float colorLerpSpeed = 4f;

    [Header("Color States")]
    [SerializeField] private ColorState[] colorStates;

    #region Private Fields
    private Material innerMat;
    private Material outerMat;

    private float currentAngle;
    private float currentDistance;
    private float currentScale;
    private Color currentInnerColor;
    private Color currentOuterColor;


    private float targetAngle;
    private float targetDistance;
    private float targetScale;
    private Color targetInnerColor;
    private Color targetOuterColor;

    private float colorLerpStartTime;
    private float paramLerpStartTime;

    private bool isParamLerping = false;
    private bool isColorLerping = false;
    #endregion

    void Awake() {
        innerMat = innerCircle.material;
        outerMat = outerCircle.material;

        innerRectTransform = innerCircle.GetComponent<RectTransform>();

        currentInnerColor = innerMat.GetColor("_Color");
        currentOuterColor = outerMat.GetColor("_Color");

        currentAngle = 40f;
        currentDistance = 5f;

        currentScale = innerRectTransform.localScale.x;

        SetVisualParameters(15f, 1.5f);
        
    }

    //getters for current visual parameters
    public float GetCurrentAngle() => currentAngle;
    public float GetCurrentDistance() => currentDistance;

    public void SetVisualColor(StateName state, bool opacityOff = false) {

        Color newColor = GetColorFromState(state);

        currentInnerColor = innerMat.GetColor("_Color");
        currentOuterColor = outerMat.GetColor("_Color");

        if (opacityOff) {
            targetInnerColor = new Color(newColor.r, newColor.g, newColor.b, 0f);
            targetOuterColor = new Color(newColor.r, newColor.g, newColor.b, 0f);
            isColorLerping = true;
            return;
        }

        targetInnerColor = new Color(newColor.r, newColor.g, newColor.b, innerOpacity);
        targetOuterColor = new Color(newColor.r, newColor.g, newColor.b, outerOpacity);
        isColorLerping = true;
        colorLerpStartTime = Time.time;
    }

    public void SetVisualParameters(float angle, float distance) {
        targetAngle = angle;
        targetDistance = distance;
        targetScale = ScaleFromUnits(distance);
        Debug.Log($"[WorkerVisualController] SetVisualParameters called with angle: {angle}, distance: {distance}, scale: {targetScale}");
        isParamLerping = true;
    }

    //helper to set visual parameters back to default
    public void SetVisualParametersToDefault() {
        var defaultData = GlobalAlarm.GetDefaultLevelData();
        SetVisualParameters(defaultData.playerDetectionAngle, defaultData.playerDetectionDistance);
    }

    void Update() {
        if (isColorLerping) {
            float tc = (Time.time - colorLerpStartTime) / colorLerpSpeed;
            tc = Mathf.Clamp01(tc);
            //color lerp
            currentInnerColor = Color.Lerp(currentInnerColor, targetInnerColor, tc);
            currentOuterColor = Color.Lerp(currentOuterColor, targetOuterColor, tc);
            innerMat.SetColor("_Color", currentInnerColor);
            outerMat.SetColor("_Color", currentOuterColor);
            if (tc >= 1f) {
                isColorLerping = false;
            }
        }

        if (isParamLerping) {
            float tp = (Time.time - paramLerpStartTime) / paramLerpSpeed;
            tp = Mathf.Clamp01(tp);

            //angle lerp
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, tp);
            
            //distance -> scale lerp
            currentScale = Mathf.Lerp(currentScale, targetScale, tp);


            if (Mathf.Abs(currentAngle - targetAngle) < 0.01f && Mathf.Abs(currentScale - targetScale) < 0.01f) {
                isParamLerping = false;
            }

            var arc = 180 - currentAngle / 2f;

            innerMat.SetFloat("_Arc1", arc);
            innerMat.SetFloat("_Arc2", arc);

            outerMat.SetFloat("_Arc1", arc);
            outerMat.SetFloat("_Arc2", arc);

            innerRectTransform.localScale = Vector3.one * currentScale;

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

    //private hlper to get vis scale from units
    private float ScaleFromUnits(float units) {
        return 0.201f * units + 0.024f;
    }

}
