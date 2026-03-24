using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
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
    internal class RestingStateService {

        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly AvatarBindingStateService bindingstateService;
        [VFAutowired] private readonly AllClipsService allClipsService;
        private readonly List<PendingClip> pendingClips = new List<PendingClip>();

        public class PendingClip {
            public AnimationClip clip;
            public string owner;
        }

        public void ApplyClipToRestingState(AnimationClip clip, string owner = null) {
            var copy = clip.Clone();
            pendingClips.Add(new PendingClip { clip = copy, owner = owner ?? globals.currentFeatureName });
            allClipsService.AddAdditionalManagedClip(copy);
        }

        public void OnPhaseChanged() {
            if (!pendingClips.Any()) return;

            var debugLog = new List<string>();
            
            foreach (var pending in pendingClips) {
                bindingstateService.ApplyClip(pending.clip, pending.owner);
                foreach (var (binding,curve) in pending.clip.GetAllCurves()) {
                    var value = curve.GetLast();
                    debugLog.Add($"{binding.path} {binding.type.Name} {binding.propertyName} = {value}\n  via {pending.owner}");
                    StoreBinding(binding, value, pending.owner);
                }
            }
            pendingClips.Clear();
            stored.Clear();
            
            Debug.Log("Resting state report:\n" + debugLog.Join('\n'));
        }

        [FeatureBuilderAction(FeatureOrder.ApplyImplicitRestingStates)]
        public void ApplyImplicitRestingStates() {
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                var path = component.owner().GetPath(avatarObject, true);
                var owner = $"{component.GetType().Name} on {path}";
                try {
                    UnitySerializationUtils.Iterate(component, visit => {
                        if (visit.field?.GetCustomAttribute<DoNotApplyRestingStateAttribute>() != null) {
                            return UnitySerializationUtils.IterateResult.Skip;
                        }
                        if (visit.value is State action) {
                            var built = actionClipService.BuildOff(action);
                            ApplyClipToRestingState(built, owner: $"{component.GetType().Name} on {path}");
                        }
                        if (visit.value is FullController fc) {
                            if (!string.IsNullOrWhiteSpace(fc.toggleParam)) {
                                var rootObj = component.owner();
                                if (fc.rootObjOverride != null) rootObj = fc.rootObjOverride;
                                var built = actionClipService.BuildOff(new State {
                                    actions = {
                                        new ObjectToggleAction { obj = rootObj, mode = ObjectToggleAction.Mode.TurnOn }
                                    }
                                });
                                ApplyClipToRestingState(built, owner: owner);
                            }
                        }
                        return UnitySerializationUtils.IterateResult.Continue;
                    });
                } catch(Exception e) {
                    throw new ExceptionWithCause($"Failed to handle {owner}", e);
                }
            }
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
    }
}
