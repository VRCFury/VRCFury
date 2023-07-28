using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Feature.Base;
using VF.Inspector;
using VF.Utils;

namespace VF.Feature {
    /**
     * This builder is in charge of changing the resting state of the avatar for all the other builders.
     * If two builders make a conflicting decision, something is wrong (perhaps the user gave conflicting instructions?)
     */
    public class RestingStateBuilder : FeatureBuilder {

        public static float MagicToggleValue = -1.452f;

        public void ApplyClipToRestingState(AnimationClip clip, bool recordDefaultStateFirst = false) {
            if (recordDefaultStateFirst) {
                var defaultsManager = GetBuilder<FixWriteDefaultsBuilder>();
                foreach (var b in clip.GetFloatBindings())
                    defaultsManager.RecordDefaultNow(b, true);
                foreach (var b in clip.GetObjectBindings())
                    defaultsManager.RecordDefaultNow(b, false);
            }

            ResetAnimatorBuilder.WithoutAnimator(avatarObject, () => { clip.SampleAnimation(avatarObject, 0); });

            foreach (var (binding,curve) in clip.GetAllCurves()) {
                HandleMaterialProperties(binding, curve);
                StoreBinding(binding, curve.GetFirst());
            }
        }

        private readonly Dictionary<EditorCurveBinding, StoredEntry> stored =
            new Dictionary<EditorCurveBinding, StoredEntry>();

        private class StoredEntry {
            public string owner;
            public FloatOrObject value;
        }

        public void StoreBinding(EditorCurveBinding binding, FloatOrObject value) {
            var owner = manager.GetCurrentlyExecutingFeatureName();
            binding = NormalizeBinding(binding);
            if (stored.TryGetValue(binding, out var otherStored)) {
                if (value != otherStored.value) {
                    throw new Exception(
                        "VRCFury was told to set the resting pose of a property to two different values.\n\n" +
                        $"Property: {binding.path} {binding.propertyName}\n\n" +
                        $"{otherStored.owner} set it to {FormatValue(otherStored.value)}\n\n" +
                        $"{owner} set it to {FormatValue(value)}");
                }
            }
            stored[binding] = new StoredEntry() {
                owner = owner,
                value = value
            };
        }

        private string FormatValue(FloatOrObject value) {
            if (value.IsFloat() && value.GetFloat() == MagicToggleValue) {
                return "(toggle)";
            }
            return value.ToString();
        }

        // Used to make sure that two instances of EditorCurveBinding equal each other,
        // even if they have different discrete settings, etc
        private EditorCurveBinding NormalizeBinding(EditorCurveBinding binding) {
            return EditorCurveBinding.FloatCurve(binding.path, binding.type, binding.propertyName);
        }

        private void HandleMaterialProperties(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            var val = curve.GetFirst();
            if (!val.IsFloat()) return;
            if (!binding.propertyName.StartsWith("material.")) return;
            var propName = binding.propertyName.Substring("material.".Length);
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(mat => {
                if (!mat.HasProperty(propName)) return mat;
                mat = mutableManager.MakeMutable(mat, false);
                mat.SetFloat(propName, val.GetFloat());
                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }

        /**
         * We allow users to have conflicts between Force Object State and other types of actions
         * This is to allow more complex usages of Toggles.
         * For instance Force Object State an object to off, then make an Object Toggle for that object,
         * and then show the toggle in rest pose. This will allow the toggle to show in rest pose, but still be off
         * in the default state.
         */
        [FeatureBuilderAction(FeatureOrder.ResetRestingStateConflictList)]
        public void ResetList() {
            stored.Clear();
        }
    }
}
