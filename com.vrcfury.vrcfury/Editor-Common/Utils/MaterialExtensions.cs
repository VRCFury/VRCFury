using UnityEngine;
using UnityEngine.Rendering;
using VF.Hooks.UnityFixes;

namespace VF.Utils {
    internal static class MaterialExtensions {
        public static ShaderPropertyType? GetPropertyType(this Material mat, string propertyName) {
            if (mat == null || mat.shader == null) return null;
            return mat.shader.GetPropertyType(propertyName);
        }

        public static bool HasPropertyFast(this Material mat, string propertyName) {
            return mat != null && mat.shader != null && mat.shader.HasProperty(propertyName);
        }

        public static bool TryGetFloatFast(this Material mat, string propertyName, out float value) {
            value = 0;
            if (!mat.HasPropertyFast(propertyName)) return false;

            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                value = mat.GetFloat(propertyName);
            }
            return true;
        }

        public static void SetFloatFast(this Material mat, string propertyName, float value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetFloat(propertyName, value);
            }
        }

        public static void SetColorFast(this Material mat, string propertyName, Color value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetColor(propertyName, value);
            }
        }

        public static void SetVectorFast(this Material mat, string propertyName, Vector4 value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetVector(propertyName, value);
            }
        }

        public static void SetTextureFast(this Material mat, string propertyName, Texture value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetTexture(propertyName, value);
            }
        }

        public static void SetTextureScaleFast(this Material mat, string propertyName, Vector2 value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetTextureScale(propertyName, value);
            }
        }

        public static void SetTextureOffsetFast(this Material mat, string propertyName, Vector2 value) {
            if (!mat.HasPropertyFast(propertyName)) return;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                mat.SetTextureOffset(propertyName, value);
            }
        }

        public static Material ApplyProperty(this Material mat, string propName, float val, string reason) {
            if (mat == null) return mat;

            var type = mat.GetPropertyType(propName);
            if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range) {
                mat.TryGetFloatFast(propName, out var oldValue);
                var newValue = val;
                if (oldValue == newValue) return mat;
                mat = mat.Clone($"{reason} changed {propName} from {oldValue} to {newValue}");
                mat.SetFloatFast(propName, newValue);
                return mat;
            }

            if (propName.Length < 2 || propName[propName.Length - 2] != '.') return mat;

            var bundleName = propName.Substring(0, propName.Length - 2);
            var bundleSuffix = propName.Substring(propName.Length - 1);
            var bundleType = mat.GetPropertyType(bundleName);
            // This is /technically/ incorrect, since if a property is missing, the proper (matching unity)
            // behaviour is that it should be set to 0. However, unity really tries to not allow you to be missing
            // one component in your animator (by deleting them all at once), so it's probably not a big deal.
            if (bundleType == ShaderPropertyType.Color) {
                mat.TryGetColorFast(bundleName, out var oldValue);
                var newValue = oldValue;
                if (bundleSuffix == "r") newValue.r = val;
                if (bundleSuffix == "g") newValue.g = val;
                if (bundleSuffix == "b") newValue.b = val;
                if (bundleSuffix == "a") newValue.a = val;
                if (oldValue == newValue) return mat;
                mat = mat.Clone($"{reason} changed {bundleName} from {oldValue} to {newValue}");
                mat.SetColorFast(bundleName, newValue);
                return mat;
            }
            if (bundleType == ShaderPropertyType.Vector) {
                mat.TryGetVectorFast(bundleName, out var oldValue);
                var newValue = oldValue;
                if (bundleSuffix == "x") newValue.x = val;
                if (bundleSuffix == "y") newValue.y = val;
                if (bundleSuffix == "z") newValue.z = val;
                if (bundleSuffix == "w") newValue.w = val;
                if (oldValue == newValue) return mat;
                mat = mat.Clone($"{reason} changed {bundleName} from {oldValue} to {newValue}");
                mat.SetVectorFast(bundleName, newValue);
                return mat;
            }
            if (bundleType == ShaderExtensions.StPropertyType && bundleName.EndsWith("_ST")) {
                var textureName = bundleName.Substring(0, bundleName.Length - 3);
                var oldScale = mat.GetTextureScaleFast(textureName);
                var oldOffset = mat.GetTextureOffsetFast(textureName);
                var newScale = oldScale;
                var newOffset = oldOffset;
                if (bundleSuffix == "x") newScale.x = val;
                if (bundleSuffix == "y") newScale.y = val;
                if (bundleSuffix == "z") newOffset.x = val;
                if (bundleSuffix == "w") newOffset.y = val;
                if (oldScale == newScale && oldOffset == newOffset) return mat;
                mat = mat.Clone($"{reason} changed {textureName} offset/scale from {oldScale},{oldOffset} to {newScale},{newOffset}");
                mat.SetTextureScaleFast(textureName, newScale);
                mat.SetTextureOffsetFast(textureName, newOffset);
                return mat;
            }

            return mat;
        }

        public static bool TryGetColorFast(this Material mat, string propertyName, out Color value) {
            value = Color.clear;
            if (!mat.HasPropertyFast(propertyName)) return false;

            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                value = mat.GetColor(propertyName);
            }
            return true;
        }

        public static bool TryGetVectorFast(this Material mat, string propertyName, out Vector4 value) {
            value = Vector4.zero;
            if (!mat.HasPropertyFast(propertyName)) return false;

            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                value = mat.GetVector(propertyName);
            }
            return true;
        }

        public static bool TryGetTextureFast(this Material mat, string propertyName, out Texture value) {
            value = null;
            if (!mat.HasPropertyFast(propertyName)) return false;
            try {
                using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                    value = mat.GetTexture(propertyName);
                }
            } catch (UnityException) {
                return false;
            }
            return value != null;
        }

        public static Vector2 GetTextureScaleFast(this Material mat, string propertyName) {
            if (!mat.HasPropertyFast(propertyName)) return Vector2.one;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                return mat.GetTextureScale(propertyName);
            }
        }

        public static Vector2 GetTextureOffsetFast(this Material mat, string propertyName) {
            if (!mat.HasPropertyFast(propertyName)) return Vector2.zero;
            using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                return mat.GetTextureOffset(propertyName);
            }
        }

    }
}
