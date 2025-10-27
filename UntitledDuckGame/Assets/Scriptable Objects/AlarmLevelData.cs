using UnityEngine;

public enum AlarmLevel {
    L0, L1, L2, L3, L4, L5, L6, L7
}


[CreateAssetMenu(fileName = "AlarmLevelData", menuName = "Scriptable Objects/AlarmLevel")]
public class AlarmConfig : ScriptableObject
{
    [System.Serializable]
    public struct AlarmLevelData {
        public int Level;
        public float playerChaseSpeed;

        [Header("WorkerDetectionZone")]
        public float playerDetectionAngle;
        public float playerDetectionDistance;
        

        [Header("Chase Settings")]
        public float chaseTimer;

    }

    [SerializeField] private AlarmLevelData[] alarmLevels = new AlarmLevelData[7];

    public AlarmLevelData GetLevelData(AlarmLevel level) {
        int index = Mathf.Clamp((int)level, 0, alarmLevels.Length - 1);
        return alarmLevels[index];
    }
    
    public int LevelCount => alarmLevels.Length;
}
