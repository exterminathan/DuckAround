using UnityEngine;

public class DebugCanvasManager : MonoBehaviour
{

    public BTInputManager btInput;
    public 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (btInput == null) {
            Debug.LogError("BTInputManager reference is missing in DebugCanvasManager.");
            return;
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
