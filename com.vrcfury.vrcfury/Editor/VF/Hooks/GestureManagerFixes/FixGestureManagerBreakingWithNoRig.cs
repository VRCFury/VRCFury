using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.GestureManagerFixes {
    /**
     * https://github.com/BlackStartx/VRC-Gesture-Manager/issues/66
     */
    internal static class FixGestureManagerBreakingWithNoRig {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(FixGestureManagerBreakingWithNoRig),
                nameof(Prefix),
                "BlackStartX.GestureManager.Editor.Modules.Vrc3.AvatarDynamics.AvatarDynamicReset",
                "ReinstallAvatarColliders",
                warnIfMissing: false
            );
        }

        private static bool Prefix(object __0) {
            var animatorField = __0.GetType().GetField("AvatarAnimator",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (animatorField == null) return true;
            var animator = animatorField.GetValue(__0) as Animator;
            if (animator == null || animator.avatar == null) {
                Debug.Log("VRCFury is patching GM to stop it from crashing because the animator or rig is missing");
                return false;
            }
            return true;
        }
    }
}