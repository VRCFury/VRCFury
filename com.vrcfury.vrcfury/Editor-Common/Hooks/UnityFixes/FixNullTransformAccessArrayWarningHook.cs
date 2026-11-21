#if UNITY_6000_0_OR_NEWER
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * SDK buffers use empty transform slots for padding and to keep parallel arrays
     * aligned. Unity 6 warns when appending null, but assigning null by index is
     * silent. Grow the array with a hidden placeholder and immediately clear the slot.
     * https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3110-beta1-physbones-flood-play-mode-console-with-adding-null-transform-to-t
     */
    internal static class FixNullTransformAccessArrayWarningHook {
        private static Transform placeholder;
        private static int editorThreadId;

        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(TransformAccessArray).GetMethod(nameof(TransformAccessArray.Add), new[] { typeof(Transform) }),
                (typeof(FixNullTransformAccessArrayWarningHook), nameof(Prefix))
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            editorThreadId = Thread.CurrentThread.ManagedThreadId;
            Reflection.Patch.apply();
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                if (placeholder != null) Object.DestroyImmediate(placeholder.gameObject);
            };
        }

        private static bool Prefix(ref TransformAccessArray __instance, Transform __0) {
            if (Thread.CurrentThread.ManagedThreadId != editorThreadId || __0 != null) return true;
            var index = __instance.length;
            if (placeholder == null) {
                placeholder = EditorUtility.CreateGameObjectWithHideFlags(
                    "VRCFury Transform Placeholder",
                    HideFlags.HideAndDontSave
                ).transform;
            }
            // A non-null transform passes through this prefix to the original Add.
            __instance.Add(placeholder);
            __instance[index] = null;
            return false;
        }
    }
}
#endif
