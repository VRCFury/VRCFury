using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class ControllerManager {
        private const string BaseAvatarOwner = "Base Avatar";
        private readonly VFController ctrl;
        private readonly Func<ParamManager> paramManager;
        private readonly VRCAvatarDescriptor.AnimLayerType type;
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureNameProvider;
        private readonly Func<string> currentFeatureClipPrefixProvider;
        private readonly string tmpDir;
        // These can't use AnimatorControllerLayer, because AnimatorControllerLayer is generated on request, not consistent
        private readonly HashSet<AnimatorStateMachine> managedLayers = new HashSet<AnimatorStateMachine>();
        private readonly Dictionary<AnimatorStateMachine, string> layerOwners =
            new Dictionary<AnimatorStateMachine, string>();
        private readonly List<AvatarMask> unionedBaseMasks = new List<AvatarMask>();
    
        public ControllerManager(
            VFController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            Func<string> currentFeatureClipPrefixProvider,
            string tmpDir,
            bool treatAsManaged = false
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.currentFeatureClipPrefixProvider = currentFeatureClipPrefixProvider;
            this.tmpDir = tmpDir;

            var layer0 = ctrl.GetLayer(0);
            if (layer0 != null) {
                unionedBaseMasks.Add(layer0.mask);
                layer0.weight = 1;
            }

            foreach (var layer in ctrl.layers) {
                layerOwners[layer.stateMachine] = BaseAvatarOwner;
                if (treatAsManaged) {
                    managedLayers.Add(layer.stateMachine);
                }
            }
        }

        public VFController GetRaw() {
            return ctrl;
        }

        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return type;
        }

        public VFLayer EnsureEmptyBaseLayer() {
            var oldLayer0 = ctrl.GetLayer(0);
            if (oldLayer0 != null && oldLayer0.stateMachine.defaultState == null) {
                return oldLayer0;
            }
            var newLayer0 = NewLayer("Base Mask", insertAt: 0, hasOwner: false);
            if (oldLayer0 != null) {
                newLayer0.mask = oldLayer0.mask;
            }
            return newLayer0;
        }

        public VFLayer NewLayer(string name, int insertAt = -1, bool hasOwner = true) {
            var newLayer = ctrl.NewLayer(NewLayerName(name), insertAt);
            managedLayers.Add(newLayer.GetRawStateMachine());
            if (hasOwner) {
                layerOwners[newLayer.GetRawStateMachine()] = currentFeatureNameProvider();
            }
            return newLayer;
        }
        
        private AnimationClip _noopClip;
        public AnimationClip GetEmptyClip() {
            if (_noopClip == null) {
                _noopClip = _NewClip("VFempty");
            }
            return _noopClip;
        }
        public string NewClipName(string name) {
            return $"{currentFeatureClipPrefixProvider.Invoke()}/{name}";
        }
        public AnimationClip NewClip(string name) {
            return _NewClip(NewClipName(name));
        }
        private AnimationClip _NewClip(string name) {
            var clip = new AnimationClip { name = name };
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            return clip;
        }
        public BlendTree NewBlendTree(string name) {
            return _NewBlendTree(NewClipName(name));
        }
        private BlendTree _NewBlendTree(string name) {
            var tree = new BlendTree { name = name };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            return tree;
        }

        public void RemoveLayer(AnimatorStateMachine sm) {
            var layer = ctrl.GetLayer(sm);
            var id = layer.GetLayerId();
            managedLayers.Remove(sm);
            layerOwners.Remove(sm);
            ctrl.RemoveLayer(id);
        }

        /**
         * BEWARE: This consumes the ENTIRE asset file containing "other"
         * The animator controller (and its sub-assets) should be owned by vrcfury, and should
         * be the ONLY THING in that file!!!
         */
        public void TakeOwnershipOf(AnimatorController other) {
            other.layers = other.layers.Select((layer, i) => {
                if (i == 0) layer.defaultWeight = 1;
                layer.name = NewLayerName(layer.name);
                managedLayers.Add(layer.stateMachine);
                layerOwners[layer.stateMachine] = currentFeatureNameProvider();
                return layer;
            }).ToArray();
            ctrl.layers = ctrl.layers.Concat(other.layers).ToArray();

            var path = AssetDatabase.GetAssetPath(other);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                if (asset is Motion) asset.name = NewClipName(asset.name);
                if (asset is AnimatorController) continue;
                AssetDatabase.RemoveObjectFromAsset(asset);
                AssetDatabase.AddObjectToAsset(asset, ctrl);
            }
            
            AssetDatabase.DeleteAsset(path);
        }

        public string NewLayerName(string name) {
            return "[VF" + currentFeatureNumProvider() + "] " + name;
        }

        public IEnumerable<VFLayer> GetLayers() {
            return ctrl.GetLayers();
        }
        public IEnumerable<VFLayer> GetManagedLayers() {
            return GetLayers().Where(l => IsManaged(l));
        }
        public IEnumerable<VFLayer> GetUnmanagedLayers() {
            return GetLayers().Where(l => !IsManaged(l));
        }

        private bool IsManaged(AnimatorStateMachine layer) {
            return managedLayers.Contains(layer);
        }

        private ParamManager GetParamManager() {
            return paramManager.Invoke();
        }

        private static FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");
        
        public VFABool NewTrigger(string name, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            return ctrl.NewTrigger(name);
        }
        public VFABool NewBool(string name, bool synced = false, bool networkSynced = true, bool def = false, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Bool;
                param.saved = saved;
                param.defaultValue = def ? 1 : 0;
                if (networkSyncedField != null) networkSyncedField.SetValue(param, networkSynced);
                GetParamManager().addSyncedParam(param);
            }
            return ctrl.NewBool(name, def);
        }
        public VFAInteger NewInt(string name, bool synced = false, bool networkSynced = true, int def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Int;
                param.saved = saved;
                param.defaultValue = def;
                if (networkSyncedField != null) networkSyncedField.SetValue(param, networkSynced);
                GetParamManager().addSyncedParam(param);
            }
            return ctrl.NewInt(name, def);
        }
        public VFAFloat NewFloat(string name, bool synced = false, float def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Float;
                param.saved = saved;
                param.defaultValue = def;
                GetParamManager().addSyncedParam(param);
            }
            return ctrl.NewFloat(name, def);
        }

        public AnimatorControllerParameterType? GetType(string name) {
            var exists = Array.Find(ctrl.parameters, other => other.name == name);
            if (exists != null) return exists.type;
            return null;
        }
        
        private string NewParamName(string name) {
            name = NewParamName(name, currentFeatureNumProvider.Invoke());
            int offset = 1;
            while (true) {
                var attempt = name + ((offset == 1) ? "" : offset+"");
                if (!GetType(attempt).HasValue) return attempt;
                offset++;
            }
        }
        public static string NewParamName(string name, int modelNum) {
            return "VF" + modelNum + "_" + name;
        }

        public VFABool True() {
            return NewBool("VF_True", def: true, usePrefix: false);
        }
        public VFAFloat One() {
            return NewFloat("VF_One", def: 1f, usePrefix: false);
        }
        public VFAFloat Zero() {
            return NewFloat("VF_Zero", def: 0f, usePrefix: false);
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

        public void UnionBaseMask(AvatarMask sourceMask) {
            unionedBaseMasks.Add(sourceMask);
        }
        public List<AvatarMask> GetUnionBaseMasks() {
            return unionedBaseMasks;
        }

        public string GetLayerOwner(AnimatorStateMachine stateMachine) {
            if (!layerOwners.TryGetValue(stateMachine, out var layerOwner)) {
                return null;
            }
            return layerOwner;
        }

        public bool IsOwnerBaseAvatar(AnimatorStateMachine stateMachine) {
            return GetLayerOwner(stateMachine) == BaseAvatarOwner;
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
