using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    public static class MaterialLocker {
        public static VFGameObject injectedAvatarObject = null;
        
        /**
         * We need to lockdown materials (that support it) before we make a clone for several reasons:
         * 1. When we patch a shader (for instance with SPS), it may prevent the shader from finding the material
         *    later, preventing it from locking it down itself.
         * 2. If we "Apply During Upload" to an unlocked material, we may wind up being able to change values
         *    that aren't marked as animated, which would be non-deterministic. (It would work differently if the material
         *    wound up locked down first).
         * 3. If we make a clone of the unlocked material, the lockdown will have to happen during every upload, which would
         *    be slow.
         */
        public static void Lock(Material mat) {
            try {
                LockUnsafe(mat);
            } catch (Exception e) {
                throw new Exception(
                    "Failed to lock material " + mat.name + ". This usually means your Poiyomi is out of date. You can update it from https://poiyomi.github.io/vpm\n\n" + e.Message, e);
            }
        }

        private static void LockUnsafe(Material mat) {
            // If the avatar is setup to use d4rk optimizer lockdown, we SHOULD NOT lock it using poi, because it's intended
            // for all the mats to remain unlocked until d4rk locks them at the end of the build
            if (UsesD4rk(injectedAvatarObject, true)) return;

            LockPoiyomi(mat);
        }
        
        public static bool UsesD4rk(VFGameObject avatarObject, bool andLockdown) {
            if (avatarObject == null) return false;
            var d4rkOptimizerType = ReflectionUtils.GetTypeFromAnyAssembly("d4rkAvatarOptimizer");
            if (d4rkOptimizerType == null) return false;
            var optimizers = avatarObject.GetComponentsInSelfAndChildren(d4rkOptimizerType);

            if (andLockdown) {
                var lockProp = d4rkOptimizerType.GetProperty("WritePropertiesAsStaticValues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (lockProp == null) return false;
                return optimizers.Any(o => (bool)lockProp.GetValue(o));
            } else {
                return optimizers.Any();
            }
        }

        private static void LockPoiyomi(Material mat) {
            if (mat.shader == null) return;
            if (mat.shader.name.StartsWith("Hidden/Locked/")) return;

            var optimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
            if (optimizer == null) return;

            var usesMethod = optimizer.GetMethod(
                "IsShaderUsingThryOptimizer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            );
            if (usesMethod == null) return;
            var usesPoi = (bool)ReflectionUtils.CallWithOptionalParams(usesMethod, null, mat.shader);
            if (!usesPoi) return;

            var lockMethod = optimizer.GetMethod(
                "SetLockedForAllMaterials",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            );
            if (lockMethod == null) return;
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                var result =
                    (bool)ReflectionUtils.CallWithOptionalParams(lockMethod, null, new Material[] { mat }, 1);
                if (!result) {
                    throw new Exception(
                        "Poiyomi's lockdown method returned false without an exception. Check the console for the reason.");
                }
            });

            if (!mat.shader.name.StartsWith("Hidden/Locked/")) {
                throw new Exception(
                    "Failed to lockdown poi material. Try unlocking and relocking the material manually. If that doesn't work, try updating poiyomi.");
            }
        }
    }
}
