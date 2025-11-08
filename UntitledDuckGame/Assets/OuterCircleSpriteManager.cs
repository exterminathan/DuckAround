using UnityEngine;

public class OuterCircleSpriteManager : MonoBehaviour
{
    [SerializeField] private SpriteRenderer outerCircleSprite;
    [SerializeField] private Sprite[] circleSprites; // 0 -> 4 : thickest to thinnest
    void Awake() {
        if (outerCircleSprite == null)
            outerCircleSprite = GetComponent<SpriteRenderer>();

    }
    
    public void SetCircleThickness(float value) {
        float maxDistance = GlobalAlarm.GetMaxAlarmLevelData().playerDetectionDistance;
        var index = Mathf.Clamp(Mathf.FloorToInt((1f - (value / maxDistance)) * circleSprites.Length), 0, circleSprites.Length - 1);
        outerCircleSprite.sprite = circleSprites[index];

        //Debug.Log($"Calculated max distance: {maxDistance}");
    }

}
