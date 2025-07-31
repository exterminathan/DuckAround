using FMODUnity;
using UnityEngine;

public class FMODEvents : MonoBehaviour {

    [field: Header("Ragdoll SFX")]
    [field: SerializeField] public EventReference ragdollSound { get; private set; }


    public static FMODEvents instance { get; private set; }

    private void Awake() {
        if (instance != null) {
            Debug.LogError("More than one FMODEvents in scene.");
        }
        instance = this;
    }
}
