#if UNITY_2022_3_6
using System;
using System.Reflection;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Backports Unity's cleanup for a ListView that updates after its SerializedObject has been disposed.
     * Older Unity releases leave the binding active and throw again on each update.
     */
    internal static class ReleaseDisposedListBindingHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type Binding = ReflectionUtils.GetTypeFromAnyAssembly(
                "UnityEditor.UIElements.Bindings.ListViewSerializedObjectBinding"
            );
            public static readonly MethodInfo Release = Binding?.VFMethod("Release");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(ReleaseDisposedListBindingHook),
                nameof(Finalizer),
                Binding,
                "Update",
                HarmonyUtils.PatchMode.Finalizer
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static Exception Finalizer(object __instance, Exception __exception) {
            if (__exception is NullReferenceException
                && __exception.Message.Contains("SerializedObject of SerializedProperty has been Disposed.")) {
                Reflection.Release.Invoke(__instance, null);
                return null;
            }
            return __exception;
        }
    }
}
#endif
