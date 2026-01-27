using UnityEngine;
using TMPro;

public class AlarmManager : MonoBehaviour
{
    [SerializeField] private TMP_Text alarmText;
    private AlarmLevel cachedAlarm = AlarmLevel.L0;


	// Update is called once per frame
	void Update()
    {
        AlarmLevel currentLevel = GlobalAlarm.Level;
        if (currentLevel != cachedAlarm)
        {
            cachedAlarm = currentLevel;
            alarmText.text = GlobalAlarm.LevelToString();
        }
    }
}
