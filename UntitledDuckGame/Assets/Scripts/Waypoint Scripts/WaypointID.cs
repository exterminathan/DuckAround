using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class WaypointID : MonoBehaviour {
    public int waypointID = 0;
    private GUIStyle guiStyle;

    void Start() {

    }

    private void OnValidate() {
        gameObject.name = $"Waypoint - {waypointID}";
    }

    private void OnDrawGizmos() {
#if UNITY_EDITOR
        if (guiStyle == null) {
            guiStyle = new GUIStyle();
            guiStyle.fontSize = 20;
            guiStyle.fontStyle = FontStyle.Bold;
            guiStyle.normal.textColor = Color.white;
            guiStyle.alignment = TextAnchor.MiddleCenter;
        }

        Handles.Label(transform.position + Vector3.up * 1, $"{waypointID}", guiStyle);
#endif
    }

}
