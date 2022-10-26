using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VF.Builder {
    public class ControllerManager {
        private static string prefix = "VRCFury";
        private readonly AnimatorController ctrl;
        private readonly ParamManager paramManager;
        private VRCAvatarDescriptor.AnimLayerType type;

        public ControllerManager(
            AnimatorController ctrl,
            ParamManager paramManager,
            VRCAvatarDescriptor.AnimLayerType type
        ) {
            this.ctrl = ctrl;
            this.paramManager = paramManager;
            this.type = type;
        }

        public AnimatorController GetRaw() {
            return ctrl;
        }

        private VFAController _controller;
        private VFAController GetController() {
            if (_controller == null) _controller = new VFAController(GetRaw(), type);
            return _controller;
        }

        public VFALayer NewLayer(string name, int insertAt = -1) {
            return GetController().NewLayer(NewLayerName(name), insertAt);
        }

        public static string NewLayerName(string name) {
            return "[" + prefix + "] " + name;
        }

        public IEnumerable<AnimatorControllerLayer> GetManagedLayers() {
            return GetRaw().layers.Where(l => l.name.StartsWith("[" + prefix + "] "));
        }
        public IEnumerable<AnimatorControllerLayer> GetUnmanagedLayers() {
            return GetRaw().layers.Where(l => !l.name.StartsWith("[" + prefix + "] "));
        }

        public VFABool NewTrigger(string name, bool usePrefix = true) {
            if (usePrefix) name = NewParamName(name);
            return GetController().NewTrigger(name);
        }
        public VFABool NewBool(string name, bool synced = false, bool def = false, bool saved = false, bool usePrefix = true, bool defTrueInEditor = false) {
            if (usePrefix) name = NewParamName(name);
            if (synced) {
                var param = new VRCExpressionParameters.Parameter();
                param.name = name;
                param.valueType = VRCExpressionParameters.ValueType.Bool;
                param.saved = saved;
                param.defaultValue = def ? 1 : 0;
                paramManager.addSyncedParam(param);
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
                paramManager.addSyncedParam(param);
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
                paramManager.addSyncedParam(param);
            }
            return GetController().NewFloat(name, def);
        }
        public static string NewParamName(string name) {
            return prefix + "__" + name;
        }

        public static void PurgeFromAnimator(AnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            // Clean up layers
            for (var i = 0; i < ctrl.layers.Length; i++) {
                var layer = ctrl.layers[i];
                if (layer.name.StartsWith("["+prefix+"]")) {
                    RemoveLayer(ctrl, i, type);
                    i--;
                }
            }
            // Clean up parameters
            for (var i = 0; i < ctrl.parameters.Length; i++) {
                var param = ctrl.parameters[i];
                if (param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__")) {
                    ctrl.RemoveParameter(param);
                    i--;
                }
            }
        }

        public static void RemoveLayer(AnimatorController ctrl, int i, VRCAvatarDescriptor.AnimLayerType type) {
            CorrectLayerReferences(ctrl, i, type, -1);
            ctrl.RemoveLayer(i);
        }
        
        public static void CorrectLayerReferences(AnimatorController ctrl, int after, VRCAvatarDescriptor.AnimLayerType type, int offset) {
            if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                foreach (var layer in ctrl.layers) {
                    AnimatorIterator.ForEachState(layer, state => {
                        foreach (var b in state.behaviours) {
                            var layerControl = b as VRCAnimatorLayerControl;
                            if (layerControl && layerControl.playable == VRC_AnimatorLayerControl.BlendableLayer.FX && layerControl.layer > after) {
                                layerControl.layer += offset;
                            }
                        }
                    });
                }
            }
        }
        
        public VFACondition Always() {
            return NewBool("True", def: true).IsTrue();
        }
        public VFANumber GestureLeft() {
            return NewInt("GestureLeft", usePrefix: false);
        }
        public VFANumber GestureRight() {
            return NewInt("GestureRight", usePrefix: false);
        }
        public VFANumber Viseme() {
            return NewInt("Viseme", usePrefix: false);
        }
        public VFABool IsLocal() {
            return NewBool("IsLocal", usePrefix: false);
        }
    }
}