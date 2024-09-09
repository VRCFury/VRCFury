using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal class PoiyomiUtils {
        private static readonly Type ShaderOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
        private static readonly MethodInfo IsShaderUsingThryOptimizer = ShaderOptimizer?.GetMethod(
            "IsShaderUsingThryOptimizer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        private static readonly MethodInfo SetLockedForAllMaterials = ShaderOptimizer?.GetMethod(
            "SetLockedForAllMaterials",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        
        private static readonly Dictionary<Material, (HashSet<string>,HashSet<string>)> lockedPropsCache
            = new Dictionary<Material, (HashSet<string>,HashSet<string>)>();

        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(() => {
                lockedPropsCache.Clear();
            }, 0);
        }
        
        private static bool IsPoiUnlocked(Material mat) {
            if (mat == null || mat.shader == null) return false;
            if (mat.shader.name.StartsWith("Hidden/Locked/")) return false;
            if (IsShaderUsingThryOptimizer == null) return false;
            return (bool)ReflectionUtils.CallWithOptionalParams(IsShaderUsingThryOptimizer, null, mat.shader);
        }
        
        private static bool IsPoiLocked(Material mat) {
            if (mat == null || mat.shader == null) return false;
            return mat.shader.name.StartsWith("Hidden/Locked/");
        }
        
        public static bool IsPoiyomiWithPropNonanimated(Material m, string propertyName) {
            return (IsPoiLocked(m) || IsPoiUnlocked(m)) && GetLockedProps(m).Item1.Contains(propertyName);
        }

        private static (HashSet<string>,HashSet<string>) GetLockedProps(Material mat) {
            var animated = new HashSet<string>();
            var nonAnimated = new HashSet<string>();

            if (mat == null) return (nonAnimated, animated);
            var shader = mat.shader;
            if (shader == null) return (nonAnimated, animated);

            if (lockedPropsCache.TryGetValue(mat, out var cached)) return cached;

            var matRenameSuffix = mat.GetTag("thry_rename_suffix", false, "");

            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++) {
                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var animatedTag = mat.GetTag(propertyName + "Animated", false, "");

                var list = animatedTag == "" ? nonAnimated : animated;
                var renameSuffix = (animatedTag == "2" && matRenameSuffix != "") ? $"_{matRenameSuffix}" : "";

                if (propType == ShaderUtil.ShaderPropertyType.TexEnv) {
                    list.Add($"{propertyName}{renameSuffix}_ST.x");
                    list.Add($"{propertyName}{renameSuffix}_ST.y");
                    list.Add($"{propertyName}{renameSuffix}_ST.z");
                    list.Add($"{propertyName}{renameSuffix}_ST.w");
                    list.Add($"{propertyName}{renameSuffix}_TexelSize.x");
                    list.Add($"{propertyName}{renameSuffix}_TexelSize.y");
                    list.Add($"{propertyName}{renameSuffix}_TexelSize.z");
                    list.Add($"{propertyName}{renameSuffix}_TexelSize.w");
                } else if (propType == ShaderUtil.ShaderPropertyType.Vector) {
                    list.Add($"{propertyName}{renameSuffix}.x");
                    list.Add($"{propertyName}{renameSuffix}.y");
                    list.Add($"{propertyName}{renameSuffix}.z");
                    list.Add($"{propertyName}{renameSuffix}.w");
                } else if (propType == ShaderUtil.ShaderPropertyType.Color) {
                    list.Add($"{propertyName}{renameSuffix}.r");
                    list.Add($"{propertyName}{renameSuffix}.g");
                    list.Add($"{propertyName}{renameSuffix}.b");
                    list.Add($"{propertyName}{renameSuffix}.a");
                }
                list.Add($"{propertyName}{renameSuffix}");
            }

            return lockedPropsCache[mat] = (nonAnimated, animated);
        }

        public static void LockPoiyomi(Material mat) {
            if (!IsPoiUnlocked(mat)) return;

            if (SetLockedForAllMaterials == null) {
                throw new Exception("Failed to find Poiyomi's lockdown method. Try updating poiyomi or locking the material manually.");
            }
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                var result =
                    (bool)ReflectionUtils.CallWithOptionalParams(SetLockedForAllMaterials, null, new Material[] { mat }, 1);
                if (!result) {
                    throw new Exception("Poiyomi's lockdown method returned false without an exception. Check the console for the reason.");
                }
            });

            if (!mat.shader.name.StartsWith("Hidden/Locked/")) {
                throw new Exception("Failed to lockdown poi material. Try unlocking and relocking the material manually. If that doesn't work, try updating poiyomi.");
            }
        }
    }
}
