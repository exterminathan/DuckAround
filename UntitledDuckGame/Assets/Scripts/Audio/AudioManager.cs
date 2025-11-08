using FMODUnity;
using FMOD.Studio;
using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour {
    public static AudioManager instance { get; private set; }
    [SerializeField] private FMODEvents events;

    private Dictionary<string, EventReference> eventMap;

    private void Awake() {
        if (instance != null) {
            Debug.LogError("More than one AudioManager");
        }
        instance = this;

        eventMap = new Dictionary<string, EventReference> {
            {"ragdoll", events.ragdollSound},
            {"playerHitWorker", events.collisionSound}
        };
    }

    public EventReference Get(string key) {
        return eventMap[key];
    }
}
