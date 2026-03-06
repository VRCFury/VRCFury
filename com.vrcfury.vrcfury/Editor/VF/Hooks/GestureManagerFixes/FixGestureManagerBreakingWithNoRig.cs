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
        [ReflectionHelperOptional]
        private abstract class GmReflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixGestureManagerBreakingWithNoRig),
                nameof(Prefix),
                "BlackStartX.GestureManager.Editor.Modules.Vrc3.AvatarDynamics.AvatarDynamicReset",
                "ReinstallAvatarColliders"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<GmReflection>()) return;
            GmReflection.Patch.apply();
        }

        private static bool Prefix(object __0) {
            var animatorField = __0.GetType().VFField("AvatarAnimator");
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
