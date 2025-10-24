using UnityEngine;
using TMPro;

public class AlarmManager : MonoBehaviour
{
    [SerializeField] private TMP_Text alarmText;
    private float cachedAlarm = 0f;


	// Update is called once per frame
	void Update()
    {
        float lvl = GlobalAlarm.Level; 
        if (lvl != cachedAlarm)
        {
            cachedAlarm = lvl;
            alarmText.text = lvl.ToString("F1");
        }
    }
}
