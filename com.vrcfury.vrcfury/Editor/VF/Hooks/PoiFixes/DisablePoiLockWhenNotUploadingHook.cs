using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.PoiFixes {
    internal static class DisablePoiLockWhenNotUploadingHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type LockMaterialsOnUpload =
                PoiyomiUtils.ShaderOptimizer?.VFNestedType("LockMaterialsOnUpload");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;

            HarmonyUtils.Patch(
                typeof(DisablePoiLockWhenNotUploadingHook),
                nameof(Prefix),
                Reflection.LockMaterialsOnUpload,
                "OnPreprocessAvatar",
                warnIfMissing: false
            );
        }

        private static bool Prefix(ref bool __result, object __instance, GameObject __0) {
            if (!IsActuallyUploadingHook.Get()) {
                Debug.Log($"VRCFury inhibited {__instance.GetType().FullName} from running because an upload isn't actually happening");
                __result = true;
                return false;
            }
            return true;
        }
    }
}

