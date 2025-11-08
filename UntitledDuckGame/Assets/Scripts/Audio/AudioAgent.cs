using UnityEngine;
using FMODUnity;

public class AudioAgent : MonoBehaviour
{
    private StudioEventEmitter[] emitters;

    void Awake() {
        emitters = GetComponents<StudioEventEmitter>();
    }

    public void Play(string key) {
        var evt = AudioManager.instance.Get(key);
        PlayEvent(evt);
    }

    public void PlayEvent(EventReference evt) {
        foreach (var emitter in emitters) {
            if (emitter.EventReference.Guid == evt.Guid) {
                emitter.Play();
                return;
            }
        }

        //fallback to dynamically assign emitter if none found, don't by default
    }
}
