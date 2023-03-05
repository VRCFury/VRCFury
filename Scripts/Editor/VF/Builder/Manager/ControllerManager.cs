using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class ControllerManager {
        private readonly AnimatorController ctrl;
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
        private readonly VFAController _controller;
    
        public ControllerManager(
            AnimatorController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            Func<string> currentFeatureClipPrefixProvider,
            string tmpDir
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.currentFeatureClipPrefixProvider = currentFeatureClipPrefixProvider;
            this.tmpDir = tmpDir;
            this._controller = new VFAController(ctrl, type);

            if (ctrl.layers.Length == 0) {
                // There was no base layer, just make one
                GetController().NewLayer("Base Mask");
            }
            
            foreach (var layer in ctrl.layers) {
                layerOwners[layer.stateMachine] = "Base Avatar";
            }
            
            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture && GetMask(0) == null) {
                var mask = new AvatarMask();
                for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                    mask.SetHumanoidBodyPartActive(bodyPart, false);
                }
                VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "gestureMask");
                SetMask(0, mask);
            }
        }

        public AnimatorController GetRaw() {
            return ctrl;
        }

        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return type;
        }
        
        private VFAController GetController() {
            return _controller;
        }

        public VFALayer NewLayer(string name, int insertAt = -1) {
            var newLayer = GetController().NewLayer(NewLayerName(name), insertAt);
            managedLayers.Add(newLayer.GetRawStateMachine());
            layerOwners[newLayer.GetRawStateMachine()] = currentFeatureNameProvider();
            return newLayer;
        }
        
        private AnimationClip _noopClip;
        public AnimationClip GetNoopClip() {
            if (_noopClip == null) {
                _noopClip = _NewClip("VFnoop");
                _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,0,0));
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
            var id = GetLayerId(sm);
            managedLayers.Remove(sm);
            layerOwners.Remove(sm);
            GetController().RemoveLayer(id);
        }

        public string GetLayerName(AnimatorStateMachine sm) {
            return ctrl.layers[GetLayerId(sm)].name;
        }

        public int GetLayerId(AnimatorStateMachine sm) {
            return GetLayers()
                .Select((s, i) => (s, i))
                .Where(tuple => tuple.Item1 == sm)
                .Select(tuple => tuple.Item2)
                .First();
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

        public IEnumerable<AnimatorStateMachine> GetLayers() {
            return ctrl.layers.Select(l => l.stateMachine);
        }
        public IEnumerable<AnimatorStateMachine> GetManagedLayers() {
            return GetLayers().Where(IsManaged);
        }
        public IEnumerable<AnimatorStateMachine> GetUnmanagedLayers() {
            return GetLayers().Where(l => !IsManaged(l));
        }

        private bool IsManaged(AnimatorStateMachine layer) {
            return managedLayers.Contains(layer);
        }

        public VFABool NewTrigger(string name, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            return GetController().NewTrigger(name);
        }

        private ParamManager GetParamManager() {
            return paramManager.Invoke();
        }
        public VFABool NewBool(string name, bool synced = false, bool def = false, bool saved = false, bool usePrefix = true, bool defTrueInEditor = false) {
            if (usePrefix) name = NewParamName(name);
            if (VRChatGlobalParams.Contains(name)) synced = false;
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Bool;
                param.saved = saved;
                param.defaultValue = def ? 1 : 0;
                GetParamManager().addSyncedParam(param);
            }
            return GetController().NewBool(name, def || defTrueInEditor);
        }
        public VFANumber NewInt(string name, bool synced = false, int def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Int;
                param.saved = saved;
                param.defaultValue = def;
                GetParamManager().addSyncedParam(param);
            }
            return GetController().NewInt(name, def);
        }
        public VFANumber NewFloat(string name, bool synced = false, float def = 0, bool saved = false, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Float;
                param.saved = saved;
                param.defaultValue = def;
                GetParamManager().addSyncedParam(param);
            }
            return GetController().NewFloat(name, def);
        }

        public AnimatorControllerParameterType GetType(string name) {
            var exists = Array.Find(ctrl.parameters, other => other.name == name);
            if (exists != null) return exists.type;
            return 0;
        }
        
        private string NewParamName(string name) {
            return NewParamName(name, currentFeatureNumProvider.Invoke());
        }
        public static string NewParamName(string name, int modelNum) {
            return "VF" + modelNum + "_" + name;
        }

        public VFABool True() {
            return NewBool("VF_True", def: true, usePrefix: false);
        }
        public VFACondition Always() {
            return True().IsTrue();
        }
        public VFACondition Never() {
            return True().IsFalse();
        }
        public VFANumber GestureLeft() {
            return NewInt("GestureLeft", usePrefix: false);
        }
        public VFANumber GestureRight() {
            return NewInt("GestureRight", usePrefix: false);
        }
        public VFANumber GestureLeftWeight() {
            return NewFloat("GestureLeftWeight", usePrefix: false);
        }
        public VFANumber GestureRightWeight() {
            return NewFloat("GestureRightWeight", usePrefix: false);
        }
        public VFANumber Viseme() {
            return NewInt("Viseme", usePrefix: false);
        }
        public VFABool IsLocal() {
            return NewBool("IsLocal", usePrefix: false);
        }
        public VFABool Seated() {
            return NewBool("Seated", usePrefix: false);
        }

        public void UnionBaseMask(AvatarMask sourceMask) {
            if (sourceMask == null) return;
            var mask = GetMask(0);
            if (mask == null) return;

            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                if (sourceMask.GetHumanoidBodyPartActive(bodyPart))
                    mask.SetHumanoidBodyPartActive(bodyPart, true);
            }
            for (var i = 0; i < sourceMask.transformCount; i++) {
                if (sourceMask.GetTransformActive(i)) {
                    mask.transformCount++;
                    mask.SetTransformPath(mask.transformCount-1, sourceMask.GetTransformPath(i));
                    mask.SetTransformActive(mask.transformCount-1, true);
                }
            }
        }
        
        public AvatarMask GetMask(int layerId) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return null;
            return ctrl.layers[layerId].avatarMask;
        }
        public void SetMask(int layerId, AvatarMask mask) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return;
            var layers = ctrl.layers;
            layers[layerId].avatarMask = mask;
            ctrl.layers = layers;
            VRCFuryEditorUtils.MarkDirty(ctrl);
        }
        public void SetName(int layerId, string name) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return;
            var layers = ctrl.layers;
            layers[layerId].name = name;
            ctrl.layers = layers;
            VRCFuryEditorUtils.MarkDirty(ctrl);
        }

        public string GetLayerOwner(AnimatorStateMachine stateMachine) {
            if (!layerOwners.TryGetValue(stateMachine, out var layerOwner)) {
                return null;
            }
            return layerOwner;
        }

        public float GetWeight(AnimatorStateMachine sm) {
            return GetWeight(GetLayerId(sm));
        }
        public float GetWeight(int layerId) {
            return ctrl.layers[layerId].defaultWeight;
        }
        public void SetWeight(int layerId, float weight) {
            var layers = ctrl.layers;
            var layer = layers[layerId];
            layer.defaultWeight = weight;
            ctrl.layers = layers;
        }
        public void SetWeight(AnimatorStateMachine stateMachine, float weight) {
            var layers = ctrl.layers;
            var layer = layers.FirstOrDefault(l => l.stateMachine == stateMachine);
            if (layer == null) throw new VRCFBuilderException("Failed to find layer for stateMachine");
            layer.defaultWeight = weight;
            ctrl.layers = layers;
        }

        public void ForEachClip(Action<MutableClip> action) {
            foreach (var l in GetLayers()) {
                AnimatorIterator.ForEachClip(l, clip => {
                    action(new MutableClip(clip));
                });
            }
        }

        public class MutableClip {
            private AnimationClip clip;
            
            public MutableClip(AnimationClip clip) {
                this.clip = clip;
            }
            
            public EditorCurveBinding[] GetFloatBindings() {
                return AnimationUtility.GetCurveBindings(clip);
            }
            
            public EditorCurveBinding[] GetObjectBindings() {
                return AnimationUtility.GetObjectReferenceCurveBindings(clip);
            }
            
            public AnimationCurve GetFloatCurve(EditorCurveBinding binding) {
                return AnimationUtility.GetEditorCurve(clip, binding);
            }
            
            public ObjectReferenceKeyframe[] GetObjectCurve(EditorCurveBinding binding) {
                return AnimationUtility.GetObjectReferenceCurve(clip, binding);
            }

            public void SetFloatCurve(EditorCurveBinding binding, AnimationCurve curve) {
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            
            public void SetObjectCurve(EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
            }
        }

        private static HashSet<string> VRChatGlobalParams = new HashSet<string> {
            "IsLocal",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "AvatarVersion",
            "GroundProximity"
        };
    }
}
