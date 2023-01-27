using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class ControllerManager {
        private readonly AnimatorController ctrl;
        private readonly Func<ParamManager> paramManager;
        private readonly VRCAvatarDescriptor.AnimLayerType type;
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureNameProvider;
        private readonly HashSet<AvatarMask> managedMasks = new HashSet<AvatarMask>();
        private readonly string tmpDir;
        // These can't use AnimatorControllerLayer, because AnimatorControllerLayer is generated on request, not consistent
        private readonly HashSet<AnimatorStateMachine> managedLayers = new HashSet<AnimatorStateMachine>();
        private readonly Dictionary<AnimatorStateMachine, string> layerOwners =
            new Dictionary<AnimatorStateMachine, string>();
    
        public ControllerManager(
            AnimatorController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            string tmpDir
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.tmpDir = tmpDir;

            if (ctrl.layers.Length == 0) {
                // There was no base layer, just make one
                GetController().NewLayer("Base Mask");
            } else if (ctrl.layers[0].stateMachine.entryTransitions.Length > 0) {
                // The base layer has stuff in it?
                GetController().NewLayer("Base Mask", 0);
                SetMask(0, GetMask(1));
                SetMask(1, null);
            } else {
                SetName(0, "Base Mask");
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
            if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                SetMask(0, null);
            }
        }

        public AnimatorController GetRaw() {
            return ctrl;
        }

        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return type;
        }

        private VFAController _controller;
        private VFAController GetController() {
            if (_controller == null) _controller = new VFAController(ctrl, type);
            return _controller;
        }

        public VFALayer NewLayer(string name, int insertAt = -1) {
            var newLayer = GetController().NewLayer(NewLayerName(name), insertAt);
            managedLayers.Add(newLayer.GetRawStateMachine());
            layerOwners[newLayer.GetRawStateMachine()] = currentFeatureNameProvider();
            return newLayer;
        }

        private string NewLayerName(string name) {
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

        public VFACondition Always() {
            return NewBool("VF_True", def: true, usePrefix: false).IsTrue();
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
        
        public void ModifyMask(int layerId, Action<AvatarMask> makeChanges) {
            var mask = GetMask(layerId);
            if (mask == null) {
                return;
            }
            
            if (managedMasks.Contains(mask)) {
                makeChanges(mask);
                EditorUtility.SetDirty(mask);
                return;
            }

            var copy = CloneMask(mask);
            makeChanges(copy);
            if (MasksEqual(mask, copy)) {
                return;
            }
            
            managedMasks.Add(copy);
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
            ModifyMask(0, mask => {
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
            });
        }
        
        private AvatarMask GetMask(int layerId) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return null;
            return ctrl.layers[layerId].avatarMask;
        }
        private void SetMask(int layerId, AvatarMask mask) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return;
            var layers = ctrl.layers;
            layers[layerId].avatarMask = mask;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
        }
        private void SetName(int layerId, string name) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return;
            var layers = ctrl.layers;
            layers[layerId].name = name;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
        }

        public IList<string> GetLayerOwners() {
            return layerOwners.Values.Distinct().ToList();
        }
        public string GetLayerOwner(int layerId) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return null;
            return GetLayerOwner(ctrl.layers[layerId].stateMachine);
        }
        public string GetLayerOwner(AnimatorStateMachine stateMachine) {
            if (!layerOwners.TryGetValue(stateMachine, out var layerOwner)) {
                return null;
            }
            return layerOwner;
        }
    }
}
