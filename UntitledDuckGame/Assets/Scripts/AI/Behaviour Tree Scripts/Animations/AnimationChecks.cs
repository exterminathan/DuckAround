using System.Collections.Generic;
using UnityEngine;

public static class AnimationChecks {
    public static bool IsInWalkingState(Dictionary<string, object> state) {
        if (!state.TryGetValue("Animator", out var obj)) return false;
        var animator = obj as Animator;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        //Debug.Log("in walking state check");
        return info.IsName("walk");
    }

    public static bool IsInIdleState(Dictionary<string, object> state) {
        if (!state.TryGetValue("Animator", out var obj)) return false;
        var animator = obj as Animator;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName("idle");
    }
}
