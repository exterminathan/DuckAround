using UnityEngine;

public class GlobalAlarm: MonoBehaviour {
    public static float Level { get; private set; }
    [SerializeField] private float decayRate = 0.01f;
    [SerializeField] private float cooldownDuration = 7.5f;

    private static float lastIncreaseTime = -Mathf.Infinity;
    private static GlobalAlarm instance;

    void Awake() {
        if (instance == null) {
            instance = this;
        }
        else {
            Destroy(this);
        }

    }

    public static void RequestIncrease(float amount) {
        if (instance == null) return;

        Debug.Log($"[GlobalAlarm] Time: {Time.time}, Last Increase Time: {lastIncreaseTime}, Cooldown Duration: {instance.cooldownDuration}");
        if (Time.time - lastIncreaseTime >= instance.cooldownDuration) {
            Level += amount;
            lastIncreaseTime = Time.time;
            Debug.Log($"[GlobalAlarm] Alarm increased by {amount}, new level: {Level}");
        }
        else {
            Debug.Log($"[GlobalAlarm] Alarm increase request denied due to cooldown.");
        }
    }

    void Update() {
        if (Level > 0f) {
            Level -= decayRate * Time.deltaTime;
            if (Level < 0f) Level = 0f;
        }
    }
    
}
    
