using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    /**
     * This builder is in charge of changing the resting state of the avatar for all the other builders.
     * If two builders make a conflicting decision, something is wrong (perhaps the user gave conflicting instructions?)
     */
    public class RestingStateBuilder : FeatureBuilder {

        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FixWriteDefaultsBuilder writeDefaultsManager;
        private readonly List<PendingClip> pendingClips = new List<PendingClip>();

        public class PendingClip {
            public AnimationClip clip;
            public string owner;
        }

        public void ApplyClipToRestingState(AnimationClip clip, bool recordDefaultStateFirst = false) {
            if (recordDefaultStateFirst) {
                foreach (var b in clip.GetFloatBindings())
                    writeDefaultsManager.RecordDefaultNow(b, true);
                foreach (var b in clip.GetObjectBindings())
                    writeDefaultsManager.RecordDefaultNow(b, false);
            }

            var copy = new AnimationClip();
            copy.CopyFrom(clip);
            pendingClips.Add(new PendingClip { clip = copy, owner = manager.GetCurrentlyExecutingFeatureName() });
            mover.AddAdditionalManagedClip(copy);
        }

        /**
         * There are three phases that resting state can be applied from,
         * (1) ForceObjectState, (2) Toggles and other things, (3) Toggle Rest Pose
         * Conflicts are allowed between phases, but not within a phase.
         */
        [FeatureBuilderAction(FeatureOrder.ApplyRestState1)]
        public void ApplyPendingClips() {
            foreach (var pending in pendingClips) {
                pending.clip.SampleAnimation(avatarObject, 0);
                foreach (var (binding,curve) in pending.clip.GetAllCurves()) {
                    HandleMaterialProperties(binding, curve);
                    StoreBinding(binding, curve.GetFirst(), pending.owner);
                }
            }
            pendingClips.Clear();
            stored.Clear();
        }
        [FeatureBuilderAction(FeatureOrder.ApplyRestState2)]
        public void ApplyPendingClips2() {
            ApplyPendingClips();
        }
        [FeatureBuilderAction(FeatureOrder.ApplyRestState3)]
        public void ApplyPendingClips3() {
            ApplyPendingClips();
        }

        public IEnumerable<AnimationClip> GetPendingClips() {
            return pendingClips.Select(pending => pending.clip);
        }

        private readonly Dictionary<EditorCurveBinding, StoredEntry> stored =
            new Dictionary<EditorCurveBinding, StoredEntry>();

        private class StoredEntry {
            public string owner;
            public FloatOrObject value;
        }

        public void StoreBinding(EditorCurveBinding binding, FloatOrObject value, string owner) {
            binding = binding.Normalize();
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
                mat = mutableManager.MakeMutable(mat, renderer.owner());
                mat.SetFloat(propName, val.GetFloat());
                return mat;
            }).ToArray();
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}
