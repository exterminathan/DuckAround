using UnityEngine;

public static class GlobalAlarm {
    public static float Level { get; private set; }

    public static void Add(float amount) {
        Level = Mathf.Max(0f, Level + amount);
        Debug.Log($"[GlobalAlarm] Alarm level increased to {Level}");
    }

    public static void Decay(float perSecond) {
        Level = Mathf.Max(0f, Level - perSecond * Time.deltaTime);
        Debug.Log($"[GlobalAlarm] Alarm level decayed to {Level}");
    }
    
}
    
