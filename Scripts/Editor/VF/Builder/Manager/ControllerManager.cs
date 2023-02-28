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
        private readonly string tmpDir;
        // These can't use AnimatorControllerLayer, because AnimatorControllerLayer is generated on request, not consistent
        private readonly HashSet<AnimatorStateMachine> managedLayers = new HashSet<AnimatorStateMachine>();
        private readonly Dictionary<AnimatorStateMachine, string> layerOwners =
            new Dictionary<AnimatorStateMachine, string>();
        private readonly ClipStorageManager clipStorage;
        private readonly VFAController _controller;
    
        public ControllerManager(
            AnimatorController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            string tmpDir,
            ClipStorageManager clipStorage
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.tmpDir = tmpDir;
            this.clipStorage = clipStorage;
            this._controller = new VFAController(ctrl, type);

            if (ctrl.layers.Length == 0) {
                // There was no base layer, just make one
                GetController().NewLayer("Base Mask");
            }
            
            for (var i = 1; i < ctrl.layers.Length; i++) {
                layerOwners[ctrl.layers[i].stateMachine] = "Base Avatar";
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

        public void RemoveLayer(AnimatorStateMachine sm) {
            var id = GetLayerId(sm);
            managedLayers.Remove(sm);
            layerOwners.Remove(sm);
            GetController().RemoveLayer(id);
        }

        public int GetLayerId(AnimatorStateMachine sm) {
            return GetLayers()
                .Select((s, i) => (s, i))
                .Where(tuple => tuple.Item1 == sm)
                .Select(tuple => tuple.Item2)
                .First();
        }

        public void TakeLayersFrom(AnimatorController other) {
            other.layers = other.layers.Select((layer, i) => {
                if (i == 0) layer.defaultWeight = 1;
                layer.name = NewLayerName(layer.name);
                managedLayers.Add(layer.stateMachine);
                layerOwners[layer.stateMachine] = currentFeatureNameProvider();
                return layer;
            }).ToArray();
            ctrl.layers = ctrl.layers.Concat(other.layers).ToArray();
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
        public VFANumber VRCEmote() {
            return NewInt("VRCEmote", usePrefix: false);
        }
        public VFABool IsLocal() {
            return NewBool("IsLocal", usePrefix: false);
        }
        public VFABool Seated() {
            return NewBool("Seated", usePrefix: false);
        }
         public VFABool AFK() {
            return NewBool("AFK", usePrefix: false);
        }
        
        public void ModifyMask(int layerId, Action<AvatarMask> makeChanges) {
            var mask = GetMask(layerId);
            if (mask == null) {
                return;
            }

            var copy = CloneMask(mask);
            makeChanges(copy);
            if (MasksEqual(mask, copy)) {
                return;
            }
            
            SetMask(layerId, copy);
            VRCFuryAssetDatabase.SaveAsset(copy, tmpDir, "mask");
        }

        private static AvatarMask CloneMask(AvatarMask mask) {
            var copy = new AvatarMask();
            for (AvatarMaskBodyPart index = AvatarMaskBodyPart.Root; index < AvatarMaskBodyPart.LastBodyPart; ++index)
                copy.SetHumanoidBodyPartActive(index, mask.GetHumanoidBodyPartActive(index));
            copy.transformCount = mask.transformCount;
            for (int index = 0; index < mask.transformCount; ++index) {
                copy.SetTransformPath(index, mask.GetTransformPath(index));
                copy.SetTransformActive(index, mask.GetTransformActive(index));
            }
            return copy;
        }
        private static bool MasksEqual(AvatarMask a, AvatarMask b) {
            for (AvatarMaskBodyPart index = AvatarMaskBodyPart.Root; index < AvatarMaskBodyPart.LastBodyPart; ++index) {
                if (a.GetHumanoidBodyPartActive(index) != b.GetHumanoidBodyPartActive(index)) {
                    return false;
                }
            }
            if (a.transformCount != b.transformCount) return false;
            for (int index = 0; index < a.transformCount; ++index) {
                if (a.GetTransformPath(index) != b.GetTransformPath(index)) return false;
                if (a.GetTransformActive(index) != b.GetTransformActive(index)) return false;
            }
            return true;
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

        public ISet<string> GetLayerOwners() {
            return layerOwners.Values.Distinct().ToImmutableHashSet();
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
        public void RemoveLayer(int index) {
            ctrl.RemoveLayer(index);
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
