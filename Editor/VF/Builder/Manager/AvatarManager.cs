using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class AvatarManager {
        private readonly GameObject avatarObject;
        private readonly VRCAvatarDescriptor avatar;
        private readonly string tmpDir;
        private readonly Func<int> currentFeatureNumProvider;

        public AvatarManager(GameObject avatarObject, string tmpDir, Func<int> currentFeatureNumProvider) {
            this.avatarObject = avatarObject;
            this.avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            this.tmpDir = tmpDir;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
        }

        private MenuManager _menu;
        public MenuManager GetMenu() {
            if (_menu == null) {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                VRCFuryAssetDatabase.SaveAsset(menu, tmpDir, "VRCFury Menu for " + avatarObject.name);
                _menu = new MenuManager(menu, tmpDir);

                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) {
                    _menu.MergeMenu(origMenu);
                    MenuSplitter.JoinMenus(menu);
                }
                
                VRCAvatarUtils.SetAvatarMenu(avatar, menu);
            }
            return _menu;
        }

        private readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager> _controllers
            = new Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>();
        public ControllerManager GetController(VRCAvatarDescriptor.AnimLayerType type) {
            if (!_controllers.TryGetValue(type, out var output)) {
                var origFx = VRCAvatarUtils.GetAvatarController(avatar, type);
                var newPath = VRCFuryAssetDatabase.GetUniquePath(tmpDir, "VRCFury " + type + " for " + avatarObject.name, "controller");
                AnimatorController ctrl;
                if (origFx == null) {
                    ctrl = AnimatorController.CreateAnimatorControllerAtPath(newPath);
                    if (type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                        var mask = new AvatarMask();
                        foreach (var bodyPart in (AvatarMaskBodyPart[])Enum.GetValues(typeof(AvatarMaskBodyPart))) {
                            mask.SetHumanoidBodyPartActive(bodyPart, false);
                        }
                        VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "gestureMask");
                        SetBaseMask(ctrl, mask);
                    }
                } else {
                    ctrl = VRCFuryAssetDatabase.CopyAsset(origFx, newPath);
                }
                output = new ControllerManager(ctrl, GetParams, type, currentFeatureNumProvider);
                _controllers[type] = output;
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl);
            }
            return output;
        }
        public IEnumerable<ControllerManager> GetAllTouchedControllers() {
            return _controllers.Values;
        }
        public IEnumerable<ControllerManager> GetAllUsedControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.Item1 != null)
                .Select(c => GetController(c.Item3));
        }
        public IEnumerable<Tuple<VRCAvatarDescriptor.AnimLayerType, AnimatorController>> GetAllUsedControllersRaw() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.Item1 != null)
                .Select(c => Tuple.Create(c.Item3, c.Item1));
        }

        private ParamManager _params;
        public ParamManager GetParams() {
            if (_params == null) {
                var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
                var newPath = VRCFuryAssetDatabase.GetUniquePath(tmpDir, "VRCFury Params for " + avatarObject.name, "asset");
                VRCExpressionParameters prms;
                if (origParams == null) {
                    prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    prms.parameters = new VRCExpressionParameters.Parameter[]{};
                    AssetDatabase.CreateAsset(prms, newPath);
                } else {
                    prms = VRCFuryAssetDatabase.CopyAsset(origParams, newPath);
                }
                VRCAvatarUtils.SetAvatarParams(avatar, prms);
                _params = new ParamManager(prms);
            }
            return _params;
        }

        private ClipStorageManager _clipStorage;
        public ClipStorageManager GetClipStorage() {
            if (_clipStorage == null) {
                _clipStorage = new ClipStorageManager(tmpDir, currentFeatureNumProvider);
            }
            return _clipStorage;
        }

        public void Finish() {
            if (_menu != null) {
                MenuSplitter.SplitMenus(_menu.GetRaw());
            }

            // The VRCSDK usually builds the debug window name lookup before the avatar is built, so we have
            // to update it with our newly-added states
            foreach (var c in _controllers.Values) {
                EditorUtility.SetDirty(c.GetRaw());
                RebuildDebugHashes(c);
            }
            
            // The VRCSDK usually does this before the avatar is built
            var layers = avatar.baseAnimationLayers;
            for (var i = 0; i < layers.Length; i++) {
                var layer = layers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    var c = layer.animatorController as AnimatorController;
                    if (c && c.layers.Length > 0) {
                        layer.mask = c.layers[0].avatarMask;
                        layers[i] = layer;
                    }
                }
            }

            if (_menu != null) EditorUtility.SetDirty(_menu.GetRaw());
            if (_params != null) EditorUtility.SetDirty(_params.GetRaw());
            if (_clipStorage != null) _clipStorage.Finish();

            if (_params != null) {
                if (_params.GetRaw().CalcTotalCost() > VRCExpressionParameters.MAX_PARAMETER_COST) {
                    throw new Exception(
                        "Avatar is out of space for parameters! Used "
                        + _params.GetRaw().CalcTotalCost() + "/" + VRCExpressionParameters.MAX_PARAMETER_COST
                        + ". Delete some params from your avatar's param file, or disable some VRCFury features.");
                }
            }
        }

        /**
         * VRC calculates the animator debug map before vrcfury is invoked, so if we want our states to show up in the
         * debug panel, we need to add them to the map ourselves.
         */
        private void RebuildDebugHashes(ControllerManager manager) {
            foreach (var layer in manager.GetManagedLayers()) {
                ProcessStateMachine(layer.stateMachine, "");
                void ProcessStateMachine(AnimatorStateMachine stateMachine, string prefix) {
                    //Update prefix
                    prefix = prefix + stateMachine.name + ".";

                    //States
                    foreach (var state in stateMachine.states) {
                        VRCAvatarDescriptor.DebugHash hash = new VRCAvatarDescriptor.DebugHash();
                        string fullName = prefix + state.state.name;
                        hash.hash = Animator.StringToHash(fullName);
                        hash.name = fullName.Remove(0, layer.stateMachine.name.Length + 1);
                        avatar.animationHashSet.Add(hash);
                    }

                    foreach (var subMachine in stateMachine.stateMachines)
                        ProcessStateMachine(subMachine.stateMachine, prefix);
                }
            }
            EditorUtility.SetDirty(avatar);
        }

        public static AvatarMask GetBaseMask(AnimatorController ctrl) {
            return ctrl.layers[0]?.avatarMask;
        }
        public static void SetBaseMask(AnimatorController ctrl, AvatarMask mask) {
            var layers = ctrl.layers;
            layers[0].avatarMask = mask;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
        }
    }
}
