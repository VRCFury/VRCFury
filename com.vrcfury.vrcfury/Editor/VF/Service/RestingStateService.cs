using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using Action = VF.Model.StateAction.Action;

namespace VF.Service {
    /**
     * This service is in charge of changing the resting state of the avatar for all the other builders.
     * If two builders within a phase (FeatureOrder) make a conflicting decision,
     * something is wrong (perhaps the user gave conflicting instructions?)
     */
    [VFService]
    public class RestingStateService {

        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ActionClipService actionClipService;
        private readonly List<PendingClip> pendingClips = new List<PendingClip>();

        public class PendingClip {
            public AnimationClip clip;
            public string owner;
        }

        public void ApplyClipToRestingState(AnimationClip clip, string owner = null) {
            var copy = new AnimationClip();
            copy.CopyFrom(clip);
            pendingClips.Add(new PendingClip { clip = copy, owner = owner ?? globals.currentFeatureNameProvider() });
            mover.AddAdditionalManagedClip(copy);
        }

        public void OnPhaseChanged() {
            if (!pendingClips.Any()) return;

            var debugLog = new List<string>();
            
            foreach (var pending in pendingClips) {
                pending.clip.SampleAnimation(avatarObject, 0);
                foreach (var (binding,curve) in pending.clip.GetAllCurves()) {
                    var value = curve.GetFirst();
                    debugLog.Add($"{binding.path} {binding.type.Name} {binding.propertyName} = {value}\n  via {pending.owner}");
                    HandleMaterialSwaps(binding, value);
                    HandleMaterialProperties(binding, value);
                    StoreBinding(binding, value, pending.owner);
                }
            }
            pendingClips.Clear();
            stored.Clear();
            
            Debug.Log("Resting state report:\n" + string.Join("\n", debugLog));
        }

        [FeatureBuilderAction(FeatureOrder.ApplyImplicitRestingStates)]
        public void ApplyImplicitRestingStates() {
            foreach (var component in globals.avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                var path = component.owner().GetPath(globals.avatarObject, true);
                UnitySerializationUtils.Iterate(component, visit => {
                    if (visit.field?.GetCustomAttribute<DoNotApplyRestingStateAttribute>() != null) {
                        return UnitySerializationUtils.IterateResult.Skip;
                    }
                    if (visit.value is State action) {
                        var built = actionClipService.LoadStateAdv("", action);
                        ApplyClipToRestingState(built.implicitRestingClip, owner: $"{component.GetType().Name} on {path}");
                    }
                    return UnitySerializationUtils.IterateResult.Continue;
                });
            }
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

        private void HandleMaterialProperties(EditorCurveBinding binding, FloatOrObject val) {
            if (!val.IsFloat()) return;
            if (!binding.propertyName.StartsWith("material.")) return;
            var propName = binding.propertyName.Substring("material.".Length);
            var transform = avatarObject.Find(binding.path);
            if (!transform) return;
            if (binding.type == null || !typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) return;
            var renderer = transform.GetComponent(binding.type) as Renderer;
            if (!renderer) return;
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
