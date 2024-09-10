using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class MaterialLocker {
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

            PoiyomiUtils.LockPoiyomi(mat);
        }

        private static class D4rkReflection {
            public static readonly Type d4rkAvatarOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("d4rkAvatarOptimizer");
            public static readonly PropertyInfo WritePropertiesAsStaticValues = d4rkAvatarOptimizer?.GetProperty("WritePropertiesAsStaticValues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly PropertyInfo ApplyOnUpload = d4rkAvatarOptimizer?.GetProperty("ApplyOnUpload", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static bool IsApplyOnUpload(object o) => (bool)(ApplyOnUpload?.GetValue(o) ?? true);
            public static bool IsWritePropertiesAsStaticValues(object o) => (bool)(WritePropertiesAsStaticValues?.GetValue(o) ?? false);
        }

        public static bool UsesD4rk(VFGameObject avatarObject, bool andLockdown) {
            if (D4rkReflection.d4rkAvatarOptimizer == null) return false;

            if (avatarObject != null) {
                var optimizer = avatarObject.GetComponent(D4rkReflection.d4rkAvatarOptimizer);
                if (optimizer != null) {
                    if (!D4rkReflection.IsApplyOnUpload(optimizer)) return false;
                    if (andLockdown) {
                        return D4rkReflection.IsWritePropertiesAsStaticValues(optimizer);
                    } else {
                        return true;
                    }
                }
            }

            var PrefsPrefix = "d4rkpl4y3r_AvatarOptimizer_";
            var enabled = EditorPrefs.GetBool(PrefsPrefix + "DoOptimizeWithDefaultSettingsWhenNoComponent", false);
            if (andLockdown) {
                enabled &= EditorPrefs.GetInt(PrefsPrefix + "WritePropertiesAsStaticValues", 0) != 0;
            }
            return enabled;
        }
    }
}
