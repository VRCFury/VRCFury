using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    internal static class DisablePoiLockWhenNotUploadingHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var methodToPatch = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer")?
                .GetNestedType("LockMaterialsOnUpload")?
                .GetMethod( 
                "OnPreprocessAvatar",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new [] { typeof(GameObject) },
                null
            );
            if (methodToPatch == null || methodToPatch.ReturnType != typeof(bool)) {
                return;
            }

            var prefix = typeof(DisablePoiLockWhenNotUploadingHook).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            HarmonyUtils.Patch(methodToPatch, prefix);    
        }

        private static bool Prefix(ref bool __result, object __instance) {
            if (!IsActuallyUploadingHook.Get()) {
                Debug.Log($"VRCFury inhibited {__instance.GetType().FullName} from running because an upload isn't actually happening");
                __result = true;
                return false;
            }
            return true;
        }
    }
}
