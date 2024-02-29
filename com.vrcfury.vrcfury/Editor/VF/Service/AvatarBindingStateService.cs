using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Inspector;
using VF.Utils;

namespace VF.Service {
    /**
     * Allows setting / retrieving properties on the avatar, given the corresponding EditorCurveBinding.
     * Typically, this would be trivial, but unfortunately there are a lot of small edges cases which must be handled.
     */
    [VFService]
    public class AvatarBindingStateService {
        [VFAutowired] private GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        public void ApplyClip(AnimationClip clip) {
            clip.SampleAnimation(avatarObject, 0);
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                var value = curve.GetFirst();
                HandleMaterialSwaps(binding, value);
                HandleMaterialProperties(binding, value);
            }
        }
        
        public bool GetFloat(EditorCurveBinding binding, out float data) {
            // Unity always pulls material properties from the first material, even if it doesn't have the property.
            // We improve on this by pulling from the first material that actually has it.
            if (binding.propertyName.StartsWith("material.")) {
                var matProp = binding.propertyName.Substring("material.".Length);
                var obj = avatarObject.Find(binding.path);
                if (obj != null) {
                    if (forcedMaterialProperties.TryGetValue((obj, matProp), out var forcedValue)) {
                        data = forcedValue;
                        return true;
                    }
                    var renderer = obj.GetComponent(binding.type) as Renderer;
                    if (renderer != null) {
                        foreach (var mat in renderer.sharedMaterials.NotNull()) {
                            if (mat.HasProperty(matProp)) {
                                data = mat.GetFloat(matProp);
                                return true;
                            }
                        }
                    }
                }
            }
            return AnimationUtility.GetFloatValue(avatarObject, binding, out data);
        }
        public bool GetObject(EditorCurveBinding binding, out Object data) {
            return AnimationUtility.GetObjectReferenceValue(avatarObject, binding, out data);
        }

        private void HandleMaterialSwaps(EditorCurveBinding binding, FloatOrObject val) {
            if (val.IsFloat()) return;
            var newMat = val.GetObject() as Material;
            if (newMat == null) return;
            if (!binding.propertyName.StartsWith("m_Materials.Array.data[")) return;

            var start = "m_Materials.Array.data[".Length;
            var end = binding.propertyName.Length - 1;
            var str = binding.propertyName.Substring(start, end - start);
            if (!int.TryParse(str, out var num)) return;
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;
            renderer.sharedMaterials = renderer.sharedMaterials
                .Select((mat,i) => (i == num) ? newMat : mat)
                .ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }

        /**
         * There are some edge cases where users may want to animate a material property on a renderer that does not
         * actually /contain/ that property by default. (For instance, if they want to animate a property on a material
         * that is later material swapped to). To allow this, we allow them to set the initial value using Apply During Upload,
         * even if the property isn't actually currently present on the renderer. We store those initial values in this dictionary,
         * so that the avatar state reader can later find them here.
         *
         * We store using the gameobject instead of the renderer, as the renderer type is converted from mesh to skinned
         * during the build in some cases.
         */
        private readonly Dictionary<(VFGameObject, string), float> forcedMaterialProperties =
            new Dictionary<(VFGameObject, string), float>();
        
        private void HandleMaterialProperties(EditorCurveBinding binding, FloatOrObject val) {
            if (!val.IsFloat()) return;
            if (!binding.propertyName.StartsWith("material.")) return;
            var propName = binding.propertyName.Substring("material.".Length);
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;

            forcedMaterialProperties[(transform, propName)] = val.GetFloat();
            
            renderer.sharedMaterials = renderer.sharedMaterials.Select(mat => {
                if (mat == null) return mat;

                var type = mat.GetPropertyType(propName);
                if (type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range) {
                    mat = MutableManager.MakeMutable(mat);
                    mat.SetFloat(propName, val.GetFloat());
                    return mat;
                }

                if (propName.Length < 2 || propName[propName.Length-2] != '.') return mat;

                var bundleName = propName.Substring(0, propName.Length - 2);
                var bundleSuffix = propName.Substring(propName.Length - 1);
                var bundleType = mat.GetPropertyType(bundleName);
                // This is /technically/ incorrect, since if a property is missing, the proper (matching unity)
                // behaviour is that it should be set to 0. However, unit really tries to not allow you to be missing
                // one component in your animator (by deleting them all at once), so it's probably not a big deal.
                if (bundleType == ShaderUtil.ShaderPropertyType.Color) {
                    mat = MutableManager.MakeMutable(mat);
                    var color = mat.GetColor(bundleName);
                    if (bundleSuffix == "r") color.r = val.GetFloat();
                    if (bundleSuffix == "g") color.g = val.GetFloat();
                    if (bundleSuffix == "b") color.b = val.GetFloat();
                    if (bundleSuffix == "a") color.a = val.GetFloat();
                    mat.SetColor(propName, color);
                    return mat;
                }
                if (bundleType == ShaderUtil.ShaderPropertyType.Vector) {
                    mat = MutableManager.MakeMutable(mat);
                    var vector = mat.GetVector(bundleName);
                    if (bundleSuffix == "x") vector.x = val.GetFloat();
                    if (bundleSuffix == "y") vector.y = val.GetFloat();
                    if (bundleSuffix == "z") vector.z = val.GetFloat();
                    if (bundleSuffix == "w") vector.w = val.GetFloat();
                    mat.SetVector(propName, vector);
                    return mat;
                }

                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}