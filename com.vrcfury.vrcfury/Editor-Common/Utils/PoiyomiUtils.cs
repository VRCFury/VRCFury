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
        [ReflectionHelperOptional]
        private abstract class PoiReflection : ReflectionHelper {
            [CanBeNull]
            public static readonly Type ShaderOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ThryEditor.ShaderOptimizer")
                ?? ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
            public static readonly MethodInfo IsShaderUsingThryOptimizer = ShaderOptimizer?.VFStaticMethod("IsShaderUsingThryOptimizer");
            public static readonly MethodInfo SetLockedForAllMaterials = ShaderOptimizer?.VFStaticMethod("SetLockedForAllMaterials");
            public static readonly MethodInfo GetRenamedPropertySuffix = ShaderOptimizer?.VFStaticMethod("GetRenamedPropertySuffix");
        }

        [ReflectionHelperOptional]
        private abstract class ThryPresetsReflection : ReflectionHelper {
            public static readonly Type Presets = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ThryEditor.Presets");
            public static readonly FieldInfo KnownMaterials = Presets?.VFStaticField("KnownMaterials");
            public static readonly MethodInfo AddKnownMaterial = KnownMaterials?.FieldType
                .VFMethod("Add", new[] { typeof(string) });
            public static readonly MethodInfo SaveKnownMaterials = KnownMaterials?.FieldType
                .VFMethod("Save", new Type[] { });
        }

        [CanBeNull]
        public static Type ShaderOptimizer => PoiReflection.ShaderOptimizer;

        public static void AddToKnownMaterials(IEnumerable<string> guids) {
            if (!guids.Any()) return;
            try {
                if (!ReflectionHelper.IsReady<ThryPresetsReflection>()) return;

                var knownMaterials = ThryPresetsReflection.KnownMaterials.GetValue(null);
                if (knownMaterials == null) return;

                foreach (var guid in guids.Where(guid => !string.IsNullOrEmpty(guid)).Distinct()) {
                    ThryPresetsReflection.AddKnownMaterial.Invoke(knownMaterials, new object[] { guid });
                }
                ThryPresetsReflection.SaveKnownMaterials?.Invoke(knownMaterials, new object[] { });
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        private static readonly Dictionary<Material, Dictionary<string, PoiProp>> lockedPropsCache
            = new Dictionary<Material, Dictionary<string, PoiProp>>();

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(() => {
                lockedPropsCache.Clear();
            }, 0);
        }
        
        private static bool IsPoiUnlocked(Material mat) {
            if (mat == null || mat.shader == null) return false;
            if (mat.shader.name.StartsWith("Hidden/Locked/")) return false;
            if (PoiReflection.IsShaderUsingThryOptimizer == null) return false;
            return (bool)ReflectionUtils.CallWithOptionalParams(PoiReflection.IsShaderUsingThryOptimizer, null, mat.shader);
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
            if (PoiReflection.GetRenamedPropertySuffix == null) return null;
            return (string)PoiReflection.GetRenamedPropertySuffix.Invoke(null, new object[] { mat });
        }

        public static void LockPoiyomi(Material mat) {
            if (!IsPoiUnlocked(mat)) return;

            if (PoiReflection.SetLockedForAllMaterials == null) {
                throw new Exception("Failed to find Poiyomi's lockdown method. Try updating poiyomi or locking the material manually.");
            }
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                var result =
                    (bool)ReflectionUtils.CallWithOptionalParams(PoiReflection.SetLockedForAllMaterials, null, new Material[] { mat }, 1);
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

