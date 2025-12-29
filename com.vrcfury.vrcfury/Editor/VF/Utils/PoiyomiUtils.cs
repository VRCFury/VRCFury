using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Builder;

namespace VF.Utils {
    internal class PoiyomiUtils {
        [CanBeNull]
        public static readonly Type ShaderOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ThryEditor.ShaderOptimizer")
            ?? ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
        private static readonly MethodInfo IsShaderUsingThryOptimizer = ShaderOptimizer?.GetMethod(
            "IsShaderUsingThryOptimizer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        private static readonly MethodInfo SetLockedForAllMaterials = ShaderOptimizer?.GetMethod(
            "SetLockedForAllMaterials",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        private static readonly MethodInfo GetRenamedPropertySuffix = ShaderOptimizer?.GetMethod(
            "GetRenamedPropertySuffix",
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
            public bool animated;
        }

        private static Dictionary<string, PoiProp> GetProps(Material mat) {
            var output = new Dictionary<string, PoiProp>();

            if (mat == null) return output;
            var shader = mat.shader;
            if (shader == null) return output;

            if (lockedPropsCache.TryGetValue(mat, out var cached)) return cached;

            var matRenameSuffix = GetRenameSuffix(mat);

            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++) {
                var propertyName = shader.GetPropertyName(i);

                var ogName = propertyName;
                if (matRenameSuffix != null && ogName.EndsWith("_" + matRenameSuffix)) {
                    ogName = ogName.Substring(0, ogName.Length - matRenameSuffix.Length - 1);
                }
                
                var propType = shader.GetPropertyType(i);
                var animatedTag = mat.GetTag(ogName + "Animated", false, "");

                var isAnimated = animatedTag != "";
                var renameSuffix = animatedTag == "2" ? $"_{matRenameSuffix}" : "";
                void Add(string suffix) {
                    output[$"{ogName}{renameSuffix}{suffix}"] = new PoiProp {
                        animated = isAnimated,
                    };
                }

                if (propType == ShaderPropertyType.Texture) {
                    Add("_ST.x");
                    Add("_ST.y");
                    Add("_ST.z");
                    Add("_ST.w");
                    Add("_TexelSize.x");
                    Add("_TexelSize.y");
                    Add("_TexelSize.z");
                    Add("_TexelSize.w");
                } else if (propType == ShaderPropertyType.Vector) {
                    Add(".x");
                    Add(".y");
                    Add(".z");
                    Add(".w");
                } else if (propType == ShaderPropertyType.Color) {
                    Add(".r");
                    Add(".g");
                    Add(".b");
                    Add(".a");
                }
                Add("");
            }

            return lockedPropsCache[mat] = output;
        }

        [CanBeNull]
        public static string GetRenameSuffix(Material mat) {
            if (GetRenamedPropertySuffix == null) return null;
            return (string)GetRenamedPropertySuffix.Invoke(null, new object[] { mat });
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
