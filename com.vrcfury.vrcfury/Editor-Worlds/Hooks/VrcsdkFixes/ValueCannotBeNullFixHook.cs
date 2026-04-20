using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * void ISerializationCallbackReceiver.OnBeforeSerialize() in UdonSharpBehaviour is sometimes
     * called with this=null in some situations when assets are being reimported, spamming
     * the console with a ton of "Value cannot be null" errors. This supresses them.
     */
    internal static class ValueCannotBeNullFixHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchSerialize = HarmonyUtils.Patch(
                typeof(ValueCannotBeNullFixHook),
                nameof(Prefix),
                "UdonSharp.UdonSharpBehaviour",
                "UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize"
            );
            public static readonly HarmonyUtils.PatchObj PatchDeserialize = HarmonyUtils.Patch(
                typeof(ValueCannotBeNullFixHook),
                nameof(Prefix),
                "UdonSharp.UdonSharpBehaviour",
                "UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchSerialize.apply();
            Reflection.PatchDeserialize.apply();
        }

        private static bool Prefix(object __instance) {
            if (__instance is Object o && o == null) {
                return false;
            }

            return true;

        }
    }
}
