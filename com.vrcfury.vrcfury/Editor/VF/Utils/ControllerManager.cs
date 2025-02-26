using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal class ControllerManager : VFControllerWithVrcType {
        private readonly Func<ParamManager> paramManager;
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureClipPrefixProvider;
        private readonly Func<string, string> makeUniqueParamName;
        private readonly LayerSourceService layerSourceService;

        public ControllerManager(
            VFController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureClipPrefixProvider,
            Func<string, string> makeUniqueParamName,
            LayerSourceService layerSourceService
        ) : base(ctrl.GetRaw(), type) {
            this.paramManager = paramManager;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureClipPrefixProvider = currentFeatureClipPrefixProvider;
            this.makeUniqueParamName = makeUniqueParamName;
            this.layerSourceService = layerSourceService;
        }

        public VFLayer EnsureEmptyBaseLayer() {
            var oldLayer0 = GetLayer(0);
            if (oldLayer0 != null && oldLayer0.stateMachine.defaultState == null) {
                return oldLayer0;
            }
            var newLayer0 = NewLayer("Base Mask", insertAt: 0);
            if (oldLayer0 != null) {
                newLayer0.mask = oldLayer0.mask;
            }
            return newLayer0;
        }

        public override VFLayer NewLayer(string name, int insertAt = -1) {
            var newLayer = base.NewLayer(NewLayerName(name), insertAt);
            layerSourceService.SetSourceToCurrent(newLayer);
            return newLayer;
        }

        /**
         * BEWARE: This consumes the ENTIRE asset file containing "other"
         * The animator controller (and its sub-assets) should be owned by vrcfury, and should
         * be the ONLY THING in that file!!!
         */
        public void TakeOwnershipOf(AnimatorController other, bool putOnTop = false, bool prefix = true) {
            // Merge Layers
            if (prefix) {
                other.layers = other.layers.Select((layer, i) => {
                    layer.name = NewLayerName(layer.name);
                    return layer;
                }).ToArray();
            }

            if (putOnTop) {
                layers = other.layers.Concat(layers).ToArray();
            } else {
                layers = layers.Concat(other.layers).ToArray();
            }

            other.layers = new AnimatorControllerLayer[] { };
            
            // Merge Params
            foreach (var p in other.parameters) {
                _NewParam(p.name, p.type, n => {
                    n.defaultBool = p.defaultBool;
                    n.defaultFloat = p.defaultFloat;
                    n.defaultInt = p.defaultInt;
                });
            }

            other.parameters = new AnimatorControllerParameter[] { };
        }

        public string NewLayerName(string name) {
            return "[VF" + currentFeatureNumProvider() + "] " + name;
        }

        public IList<VFLayer> GetManagedLayers() {
            return GetLayers().Where(l => IsManaged(l)).ToArray();
        }
        public IList<VFLayer> GetUnmanagedLayers() {
            return GetLayers().Where(l => !IsManaged(l)).ToArray();
        }

        private bool IsManaged(AnimatorStateMachine layer) {
            return layerSourceService.GetSource(layer) != LayerSourceService.AvatarDescriptorSource;
        }

        private ParamManager GetParamManager() {
            return paramManager.Invoke();
        }

        public VFABool NewBool(string name, bool synced = false, bool networkSynced = true, bool def = false, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = makeUniqueParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Bool;
                param.saved = saved;
                param.defaultValue = def ? 1 : 0;
                param.SetNetworkSynced(networkSynced, true);
                GetParamManager().AddSyncedParam(param);
            }
            return _NewBool(name, def);
        }
        public VFAInteger NewInt(string name, bool synced = false, bool networkSynced = true, int def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = makeUniqueParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Int;
                param.saved = saved;
                param.defaultValue = def;
                param.SetNetworkSynced(networkSynced, true);
                GetParamManager().AddSyncedParam(param);
            }
            return _NewInt(name, def);
        }
        public VFAFloat NewFloat(string name, bool synced = false, float def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) {
                name = Regex.Replace(name, @"^VF\d+_", "");
                name = makeUniqueParamName(name);
            }
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Float;
                param.saved = saved;
                param.defaultValue = def;
                GetParamManager().AddSyncedParam(param);
            }
            return _NewFloat(name, def);
        }
        public BlendtreeMath.VFAap MakeAap(string name, float def = 0, bool usePrefix = true) {
            return new BlendtreeMath.VFAap(NewFloat(name, def: def, usePrefix: usePrefix));
        }

        private readonly int randomPrefix = (new System.Random()).Next(100_000_000, 999_999_999);

        public VFABool True() {
            return NewBool($"VF_{randomPrefix}_True", def: true, usePrefix: false);
        }
        public VFAFloat One() {
            return NewFloat($"VF_{randomPrefix}_One", def: 1f, usePrefix: false);
        }
        public VFAFloat Zero() {
            return NewFloat($"VF_{randomPrefix}_Zero", def: 0f, usePrefix: false);
        }
        public VFCondition Always() {
            return True().IsTrue();
        }
        public VFCondition Never() {
            return True().IsFalse();
        }
        public VFAInteger GestureLeft() {
            return NewInt("GestureLeft", usePrefix: false);
        }
        public VFAInteger GestureRight() {
            return NewInt("GestureRight", usePrefix: false);
        }
        public VFAFloat GestureLeftWeight() {
            return NewFloat("GestureLeftWeight", usePrefix: false);
        }
        public VFAFloat GestureRightWeight() {
            return NewFloat("GestureRightWeight", usePrefix: false);
        }
        public VFAInteger Viseme() {
            return NewInt("Viseme", usePrefix: false);
        }
        public VFABool IsLocal() {
            return NewBool("IsLocal", usePrefix: false);
        }
        public VFCondition IsMmd() {
            return NewBool("Seated", usePrefix: false).IsFalse()
                .And(NewBool("InStation", usePrefix: false).IsTrue());
        }

        [CanBeNull]
        public string GetLayerOwner(AnimatorStateMachine stateMachine) {
            return layerSourceService.GetSource(stateMachine);
        }

        public bool IsSourceAvatarDescriptor(AnimatorStateMachine stateMachine) {
            return GetLayerOwner(stateMachine) == LayerSourceService.AvatarDescriptorSource;
        }

        public void ForEachClip(Action<AnimationClip> action) {
            foreach (var clip in new AnimatorIterator.Clips().From(GetRaw())) {
                action(clip);
            }
        }

        public IImmutableSet<AnimationClip> GetClips() {
            return new AnimatorIterator.Clips().From(GetRaw());
        }
    }
}
