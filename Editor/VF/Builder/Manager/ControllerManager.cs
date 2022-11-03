using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class ControllerManager {
        private readonly AnimatorController ctrl;
        private readonly Func<ParamManager> paramManager;
        private readonly VRCAvatarDescriptor.AnimLayerType type;
        private readonly Func<int> currentFeatureNumProvider;

        public ControllerManager(
            AnimatorController ctrl,
            Func<ParamManager> paramManager,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
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
    }
}