using UnityEngine;

public class GlobalAlarm: MonoBehaviour {
    public static float Level { get; private set; }

    public static void RequestIncrease(float amount) {
        Level += amount;
    }

    void Update() {
        
    }
    
}
    
