using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {
    /**
     * Allows setting / retrieving properties on the avatar, given the corresponding EditorCurveBinding.
     * Typically, this would be trivial, but unfortunately there are a lot of small edges cases which must be handled.
     */
    [VFService]
    internal class AvatarBindingStateService {
        [VFAutowired] private GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        public void ApplyClip(AnimationClip clip) {
            var copy = clip.Clone();
            copy.FinalizeAsset();
            copy.SampleAnimation(avatarObject, 0);
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                var value = curve.GetLast();
                HandleMaterialSwaps(binding, value);
                HandleMaterialProperties(binding, value);
            }
        }

        public bool Get(EditorCurveBinding binding, bool isFloat, out FloatOrObject data, bool trustUnity = false) {
            if (isFloat) {
                var r = GetFloat(binding, out var d, trustUnity);
                data = d;
                return r;
            } else {
                var r = GetObject(binding, out var d, trustUnity);
                data = d;
                return r;
            }
        }
        
        public bool GetFloat(EditorCurveBinding binding, out float data, bool trustUnity = false) {
            // Unity always pulls material properties from the first material, even if it doesn't have the property.
            // We improve on this by pulling from the first material that actually has it.
            if (TryParseMaterialProperty(binding, out var matProp)) {
                if (!trustUnity) {
                    if (TryFindObject(binding, out var obj) &&
                        forcedMaterialProperties.TryGetValue((obj, matProp), out var forcedValue)) {
                        data = forcedValue;
                        return true;
                    }
                }

                if (TryFindComponent<Renderer>(binding, out var renderer)) {
                    if (trustUnity) {
                        // For some reason, in game, the default value only ever pulls from the first material slot
                        // However, in editor, AnimationUtility.GetFloatValue pulls from all slots. We need to replicate
                        // the in-game behaviour here so that FixWriteDefaults knows to record defaults affected by this
                        if (renderer.sharedMaterials.Length < 1 || renderer.sharedMaterials[0] == null ||
                            !renderer.sharedMaterials[0].HasProperty(matProp)) {
                            data = 0;
                            return false;
                        }
                    } else {
                        foreach (var mat in renderer.sharedMaterials.NotNull()) {
                            if (mat.HasProperty(matProp)) {
                                data = mat.GetFloat(matProp);
                                return true;
                            }
                        }
                    }
                }
            }

            try {
                return AnimationUtility.GetFloatValue(avatarObject, binding, out data);
            } catch (Exception) {
                // Unity throws a `UnityException: Invalid type` if you request an object that is actually a float or vice versa
                data = 0;
                return false;
            }
        }
        public bool GetObject(EditorCurveBinding binding, out Object data, bool trustUnity = false) {
            if (!trustUnity) {
                // Unity incorrectly says that material slots do not exist at all if the material in the slot is unset (null)
                if (TryParseMaterialSlot(binding, out var renderer, out var slotNum)) {
                    data = renderer.sharedMaterials[slotNum];
                    return true;
                }
            }

            try {
                return AnimationUtility.GetObjectReferenceValue(avatarObject, binding, out data);
            } catch (Exception) {
                // Unity throws a `UnityException: Invalid type` if you request an object that is actually a float or vice versa
                data = null;
                return false;
            }
        }

        private static bool TryParseMaterialProperty(EditorCurveBinding binding, out string propertyName) {
            if (binding.propertyName.StartsWith("material.")) {
                propertyName = binding.propertyName.Substring("material.".Length);
                return true;
            }
            propertyName = null;
            return false;
        }
        
        private bool TryFindObject(EditorCurveBinding binding, out VFGameObject obj) {
            obj = avatarObject.Find(binding.path);
            return obj != null;
        }

        private bool TryFindComponent<T>(EditorCurveBinding binding, out T component) where T : UnityEngine.Component {
            component = null;
            if (!TryFindObject(binding, out var obj)) return false;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return false;
            component = obj.GetComponent(binding.type) as T;
            return component != null;
        }

        public bool TryParseMaterialSlot(EditorCurveBinding binding, out Renderer renderer, out int slotNum) {
            renderer = null;
            slotNum = 0;
            var prefix = "m_Materials.Array.data[";
            if (!binding.propertyName.StartsWith(prefix)) return false;
            var start = prefix.Length;
            var end = binding.propertyName.Length - 1;
            var str = binding.propertyName.Substring(start, end - start);
            if (!int.TryParse(str, out slotNum)) return false;
            if (!TryFindComponent(binding, out renderer)) return false;
            if (slotNum < 0 || slotNum >= renderer.sharedMaterials.Length) return false;
            return true;
        }

        private void HandleMaterialSwaps(EditorCurveBinding binding, FloatOrObject val) {
            if (val.IsFloat()) return;
            var newMat = val.GetObject() as Material;
            if (newMat == null) return;
            if (!TryParseMaterialSlot(binding, out var renderer, out var num)) return;
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
            if (!TryParseMaterialProperty(binding, out var propName)) return;
            if (!TryFindComponent<Renderer>(binding, out var renderer)) return;

            forcedMaterialProperties[(renderer.owner(), propName)] = val.GetFloat();
            
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
                    mat.SetColor(bundleName, color);
                    return mat;
                }
                if (bundleType == ShaderUtil.ShaderPropertyType.Vector) {
                    mat = MutableManager.MakeMutable(mat);
                    var vector = mat.GetVector(bundleName);
                    if (bundleSuffix == "x") vector.x = val.GetFloat();
                    if (bundleSuffix == "y") vector.y = val.GetFloat();
                    if (bundleSuffix == "z") vector.z = val.GetFloat();
                    if (bundleSuffix == "w") vector.w = val.GetFloat();
                    mat.SetVector(bundleName, vector);
                    return mat;
                }

                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}