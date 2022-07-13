using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class ControllerManager {
        private static string prefix = "VRCFury";
        private readonly AnimatorController ctrl;
        private readonly string tmpDir;
        private Object clipStorage;
        private readonly ParamManager paramManager;

        public ControllerManager(AnimatorController ctrl, string tmpDir, ParamManager paramManager) {
            this.ctrl = ctrl;
            this.tmpDir = tmpDir;
            this.paramManager = paramManager;
        }

        public AnimatorController GetRawController() {
            return ctrl;
        }

        private VFAController _controller;
        private VFAController GetController() {
            if (_controller == null) {
                _controller = new VFAController(ctrl, GetNoopClip());
            }
            return _controller;
        }

        private AnimationClip _noopClip;
        public AnimationClip GetNoopClip() {
            if (_noopClip == null) {
                _noopClip = NewClip("noop");
                _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,0,0));
            }
            return _noopClip;
        }

        public VFALayer NewLayer(string name, bool first = false) {
            return GetController().NewLayer(NewLayerName(name), first);
        }

        public string NewLayerName(string name) {
            return "[" + prefix + "] " + name;
        }

        public IEnumerable<AnimatorControllerLayer> GetManagedLayers() {
            return ctrl.layers.Where(l => l.name.StartsWith("[" + prefix + "] "));
        }
        public IEnumerable<AnimatorControllerLayer> GetUnmanagedLayers() {
            return ctrl.layers.Where(l => !l.name.StartsWith("[" + prefix + "] "));
        }

        public void AddToClipStorage(Object asset) {
            if (clipStorage == null) {
                clipStorage = new AnimationClip();
                clipStorage.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(clipStorage, tmpDir + "/VRCF_Clips.anim");
            }
            AssetDatabase.AddObjectToAsset(asset, clipStorage);
        }

        public AnimationClip NewClip(string name) {
            var clip = new AnimationClip();
            clip.name = prefix + "/" + name;
            clip.hideFlags = HideFlags.None;
            AddToClipStorage(clip);
            return clip;
        }
        public BlendTree NewBlendTree(string name) {
            var tree = new BlendTree();
            tree.name = prefix + "/" + name;
            tree.hideFlags = HideFlags.None;
            AddToClipStorage(tree);
            return tree;
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
        public string NewParamName(string name) {
            return prefix + "__" + name;
        }

        public static void PurgeFromAnimator(AnimatorController ctrl) {
            // Clean up layers
            for (var i = 0; i < ctrl.layers.Length; i++) {
                var layer = ctrl.layers[i];
                if (layer.name.StartsWith("["+prefix+"]")) {
                    ctrl.RemoveLayer(i);
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
    }
}