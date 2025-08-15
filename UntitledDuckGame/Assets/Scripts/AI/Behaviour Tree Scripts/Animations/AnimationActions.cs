using System.Collections.Generic;
using UnityEngine;

public static class AnimationActions {
    public static bool SetWalking(Dictionary<string, object> state) {
        if (!state.TryGetValue("Animator", out var a) || !(a is Animator animator))
            return false;

        bool currWalking = animator.GetBool("isWalking");
        if (currWalking)
            return true;

        animator.SetBool("isWalking", true);
        Debug.Log("Animator isWalking set to: " + animator.GetBool("isWalking"));
        return true;
    }

    public static bool SetIdle(Dictionary<string, object> state) {
        if (!state.TryGetValue("Animator", out var a) || !(a is Animator animator))
            return false;

        bool currWalking = animator.GetBool("isWalking");
        if (!currWalking)
            return true;

        animator.SetBool("isWalking", false);
        Debug.Log("Animator isWalking set to: " + animator.GetBool("isWalking"));
        return true;
    }

    public static bool GetWalking(Dictionary<string, object> state) {
        if (!state.TryGetValue("Animator", out var a) || !(a is Animator animator))
            return false;

        return animator.GetBool("isWalking");
    }
}
