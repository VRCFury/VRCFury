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
        private readonly HashSet<AvatarMask> managedMasks = new HashSet<AvatarMask>();
        private readonly string tmpDir;

        public ControllerManager(
            AnimatorController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            string tmpDir
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.tmpDir = tmpDir;
        }

        public AnimatorController GetRaw() {
            return ctrl;
        }

        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return type;
        }

        private VFAController _controller;
        private VFAController GetController() {
            if (_controller == null) _controller = new VFAController(GetRaw(), type);
            return _controller;
        }

        public VFALayer NewLayer(string name, int insertAt = -1) {
            return GetController().NewLayer(NewLayerName(name), insertAt);
        }

        public string NewLayerName(string name) {
            return "[VF" + currentFeatureNumProvider.Invoke() + "] " + name;
        }

        public IEnumerable<AnimatorControllerLayer> GetManagedLayers() {
            return GetRaw().layers.Where(IsManaged);
        }
        public IEnumerable<AnimatorControllerLayer> GetUnmanagedLayers() {
            return GetRaw().layers.Where(l => !IsManaged(l));
        }

        public static bool IsManaged(AnimatorControllerLayer layer) {
            return layer.name.StartsWith("[VF");
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
        
        public AvatarMask GetMask(int layerId) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return null;
            return ctrl.layers[layerId].avatarMask;
        }
        public void SetMask(int layerId, AvatarMask mask) {
            if (layerId < 0 || layerId >= ctrl.layers.Length) return;
            var layers = ctrl.layers;
            layers[layerId].avatarMask = mask;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
        }
    }
}