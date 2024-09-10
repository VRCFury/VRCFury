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
        
        private static readonly Dictionary<Material, Dictionary<string, PoiProp>> lockedPropsCache
            = new Dictionary<Material, Dictionary<string, PoiProp>>();

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
            return (IsPoiLocked(m) || IsPoiUnlocked(m)) && GetProps(m).TryGetValue(propertyName, out var prop) && !prop.animated;
        }

        public class PoiProp {
            public string renamedTo;
            public bool animated;
        }

        private static Dictionary<string, PoiProp> GetProps(Material mat) {
            var output = new Dictionary<string, PoiProp>();

            if (mat == null) return output;
            var shader = mat.shader;
            if (shader == null) return output;

            if (lockedPropsCache.TryGetValue(mat, out var cached)) return output;

            var matRenameSuffix = mat.GetTag("thry_rename_suffix", false, "");

            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++) {
                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var animatedTag = mat.GetTag(propertyName + "Animated", false, "");

                var isAnimated = animatedTag != "";
                var renameSuffix = (animatedTag == "2" && matRenameSuffix != "") ? $"_{matRenameSuffix}" : "";
                void Add(string suffix) {
                    output.Add($"{propertyName}{suffix}", new PoiProp() {
                        animated = isAnimated,
                        renamedTo = $"{propertyName}{renameSuffix}{suffix}"
                    });
                }

                if (propType == ShaderUtil.ShaderPropertyType.TexEnv) {
                    Add("_ST.x");
                    Add("_ST.y");
                    Add("_ST.z");
                    Add("_ST.w");
                    Add("_TexelSize.x");
                    Add("_TexelSize.y");
                    Add("_TexelSize.z");
                    Add("_TexelSize.w");
                } else if (propType == ShaderUtil.ShaderPropertyType.Vector) {
                    Add(".x");
                    Add(".y");
                    Add(".z");
                    Add(".w");
                } else if (propType == ShaderUtil.ShaderPropertyType.Color) {
                    Add(".r");
                    Add(".g");
                    Add(".b");
                    Add(".a");
                }
                Add("");
            }

            return lockedPropsCache[mat] = output;
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
