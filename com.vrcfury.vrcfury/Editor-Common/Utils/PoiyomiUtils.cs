using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
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

        [ReflectionHelperOptional]
        private abstract class NewThryPresetsReflection : ReflectionHelper {
            public static readonly MethodInfo RegisterMaterials = ReflectionUtils
                .GetTypeFromAnyAssembly("Thry.ThryEditor.Presets")
                ?.VFStaticMethod("RegisterMaterials", new[] { typeof(IEnumerable<string>) });
        }

        [CanBeNull]
        public static Type ShaderOptimizer => PoiReflection.ShaderOptimizer;

        public static void AddToKnownMaterials(IEnumerable<string> guids) {
            if (!guids.Any()) return;
            try {
                if (ReflectionHelper.IsReady<NewThryPresetsReflection>()) {
                    NewThryPresetsReflection.RegisterMaterials.Invoke(null, new object[] { guids });
                    return;
                }
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
            if (!IsPoiLocked(m) && !IsPoiUnlocked(m)) return false;

            var component = "";
            var dot = propertyName.LastIndexOf('.');
            if (dot >= 0) {
                component = propertyName.Substring(dot + 1);
                if (component.Length != 1) return false;
                propertyName = propertyName.Substring(0, dot);
            }
            var renameSuffix = GetRenameSuffix(m);
            // Renamed bindings are animated. Unmarked properties on locked shaders are
            // still addressed by their original name, even if the shader name has a suffix.
            bool MatchesProperty(string name, bool textureComponent) {
                if (renameSuffix != null && name.EndsWith("_" + renameSuffix)) return false;
                if (m.GetTag(name + "Animated", false, "") != "") return false;
                bool MatchesShader(string shaderName) {
                    var index = m.shader.GetPropertyIndex(shaderName);
                    if (index < 0) return false;
                    var type = m.shader.GetPropertyType(index);
                    if (component == "") return true;
                    if (textureComponent) return type == ShaderPropertyType.Texture && "xyzw".Contains(component);
                    return (type == ShaderPropertyType.Vector && "xyzw".Contains(component))
                        || (type == ShaderPropertyType.Color && "rgba".Contains(component));
                }
                return MatchesShader(name) || (renameSuffix != null && MatchesShader(name + "_" + renameSuffix));
            }
            if (MatchesProperty(propertyName, false)) return true;
            if (component.Length > 0) {
                foreach (var suffix in new[] { "_ST", "_TexelSize" }) {
                    if (propertyName.EndsWith(suffix)
                        && MatchesProperty(propertyName.Substring(0, propertyName.Length - suffix.Length), true)) return true;
                }
            }
            return false;
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

