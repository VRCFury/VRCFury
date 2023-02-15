using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class AvatarManager {
        private readonly GameObject avatarObject;
        private readonly VRCAvatarDescriptor avatar;
        private readonly string tmpDir;
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureNameProvider;
        private readonly Func<int> currentMenuSortPosition;

        public AvatarManager(
            GameObject avatarObject,
            string tmpDir,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            Func<int> currentMenuSortPosition
        ) {
            this.avatarObject = avatarObject;
            this.avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            this.tmpDir = tmpDir;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.currentMenuSortPosition = currentMenuSortPosition;
        }

        private MenuManager _menu;
        public MenuManager GetMenu() {
            if (_menu == null) {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                VRCFuryAssetDatabase.SaveAsset(menu, tmpDir, "VRCFury Menu for " + avatarObject.name);
                var initializing = true;
                _menu = new MenuManager(menu, tmpDir, () => initializing ? 0 : currentMenuSortPosition());

                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) {
                    _menu.MergeMenu(origMenu);
                    MenuSplitter.JoinMenus(menu);
                }
                
                VRCAvatarUtils.SetAvatarMenu(avatar, menu);
                initializing = false;
            }
            return _menu;
        }

        private readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager> _controllers
            = new Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>();
        public ControllerManager GetController(VRCAvatarDescriptor.AnimLayerType type) {
            if (!_controllers.TryGetValue(type, out var output)) {
                var existingController = VRCAvatarUtils.GetAvatarController(avatar, type);
                var newPath = VRCFuryAssetDatabase.GetUniquePath(tmpDir, "VRCFury " + type + " for " + avatarObject.name, "controller");
                AnimatorController ctrl;
                if (existingController != null && AssetDatabase.IsMainAsset(existingController)) {
                    ctrl = VRCFuryAssetDatabase.CopyAsset(existingController, newPath);
                } else {
                    ctrl = AnimatorController.CreateAnimatorControllerAtPath(newPath);
                    if (existingController != null) {
                        ctrl.RemoveLayer(0);
                        var merger = new ControllerMerger();
                        merger.Merge(existingController, toRaw: ctrl);
                    }
                }
                output = new ControllerManager(ctrl, GetParams, type, currentFeatureNumProvider, currentFeatureNameProvider, tmpDir);
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
                if (origParams != null && AssetDatabase.IsMainAsset(origParams)) {
                    prms = VRCFuryAssetDatabase.CopyAsset(origParams, newPath);
                } else {
                    prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    prms.parameters = new VRCExpressionParameters.Parameter[]{};
                    AssetDatabase.CreateAsset(prms, newPath);
                    if (origParams != null) {
                        prms.parameters = origParams.parameters
                            .Select(prm => new VRCExpressionParameters.Parameter() {
                                name = prm.name,
                                valueType = prm.valueType,
                                defaultValue = prm.defaultValue,
                                saved = prm.saved,
                            }).ToArray();
                    }
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

        public void Finish(OverrideMenuSettings menuSettings) {
            if (_menu != null) {
                _menu.SortMenu();
                MenuSplitter.SplitMenus(_menu.GetRaw(), menuSettings);
                MenuSplitter.FixNulls(_menu.GetRaw());
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
                ProcessStateMachine(layer, "");
                void ProcessStateMachine(AnimatorStateMachine stateMachine, string prefix) {
                    //Update prefix
                    prefix = prefix + stateMachine.name + ".";

                    //States
                    foreach (var state in stateMachine.states) {
                        VRCAvatarDescriptor.DebugHash hash = new VRCAvatarDescriptor.DebugHash();
                        string fullName = prefix + state.state.name;
                        hash.hash = Animator.StringToHash(fullName);
                        hash.name = fullName.Remove(0, layer.name.Length + 1);
                        avatar.animationHashSet.Add(hash);
                    }

                    foreach (var subMachine in stateMachine.stateMachines)
                        ProcessStateMachine(subMachine.stateMachine, prefix);
                }
            }
            EditorUtility.SetDirty(avatar);
        }
    }
}
