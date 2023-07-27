using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Feature {
    /**
     * This builder is in charge of changing the resting state of the avatar for all the other builders.
     * If two builders make a conflicting decision, something is wrong (perhaps the user gave conflicting instructions?)
     */
    public class RestingStateBuilder : FeatureBuilder {

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
                StoreBinding(binding, curve);
            }
        }

        private readonly Dictionary<EditorCurveBinding, StoredEntry> stored =
            new Dictionary<EditorCurveBinding, StoredEntry>();

        private class StoredEntry {
            public string owner;
            public FloatOrObject value;
        }

        private void StoreBinding(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            var value = curve.GetFirst();
            var owner = manager.GetCurrentlyExecutingFeatureName();
            if (stored.TryGetValue(binding, out var otherStored)) {
                if (value != otherStored.value) {
                    throw new Exception(
                        "VRCFury was told to set the resting pose of a property to two different values.\n\n" +
                        $"Property: {binding.path} {binding.propertyName}\n\n" +
                        $"{otherStored.owner} set it to {otherStored.value}\n\n" +
                        $"{owner} set it to {value}");
                }
            }
            stored[binding] = new StoredEntry() {
                owner = owner,
                value = value
            };
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
                mat = mutableManager.MakeMutable(mat, true);
                mat.SetFloat(propName, val.GetFloat());
                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}
