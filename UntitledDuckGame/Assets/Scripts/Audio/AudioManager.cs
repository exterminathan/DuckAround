using FMODUnity;
using FMOD.Studio;
using UnityEngine;

public class AudioManager : MonoBehaviour {
    public static AudioManager instance { get; private set; }

    private void Awake() {
        if (instance != null) {
            Debug.LogError("More than one AudioManager");
        }
        instance = this;
    }

    public void PlayOneShot(EventReference sound, Vector3 worldPos) {
        RuntimeManager.PlayOneShot(sound, worldPos);
    }

    public EventInstance CreateEventInstance(EventReference eventReference) {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        return eventInstance;
    }
}
