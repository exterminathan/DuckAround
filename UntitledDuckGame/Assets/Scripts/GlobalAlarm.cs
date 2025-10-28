using UnityEngine;

public class GlobalAlarm : MonoBehaviour {
    public static AlarmLevel Level { get; private set; } = AlarmLevel.L0;
    [SerializeField] private float decayRate = 0.01f;
    [SerializeField] private float cooldownDuration = 7.5f;
    [SerializeField] private AlarmConfig alarmConfig;

    private static float currentValue = 0f;
    private static float lastIncreaseTime = -Mathf.Infinity;
    private static GlobalAlarm instance;

    void Awake() {
        if (instance == null) {
            instance = this;
        } else {
            Destroy(this);
        }
    }

    void Update() {
        // convert float 0–7 range into enum
        UpdateAlarmLevel();
    }

    public static void RequestIncrease(float amount) {
        if (instance == null) return;

        if (Time.time - lastIncreaseTime >= instance.cooldownDuration) {
            currentValue = Mathf.Clamp(currentValue + amount, 0f, 7f);
            lastIncreaseTime = Time.time;
            instance.UpdateAlarmLevel();

        }
    }

    private void UpdateAlarmLevel() {
        int levelIndex = Mathf.FloorToInt(currentValue);
        levelIndex = Mathf.Clamp(levelIndex, 0, 7);
        Level = (AlarmLevel)levelIndex;
    }

    public static AlarmConfig.AlarmLevelData GetCurrentLevelData() {
        if (instance == null || instance.alarmConfig == null) {
            Debug.LogError("GlobalAlarm: No instance or AlarmConfig assigned!");
            return default;
        }

        return instance.alarmConfig.GetLevelData(Level);
    }

    public static AlarmConfig.AlarmLevelData GetDefaultLevelData() {
        if (instance == null || instance.alarmConfig == null) {
            Debug.LogError("GlobalAlarm: No instance or AlarmConfig assigned!");
            return default;
        }

        return instance.alarmConfig.GetLevelData(AlarmLevel.L0);
    }

    public static string LevelToString() {
        return Level.ToString().Substring(1);
    }
}
