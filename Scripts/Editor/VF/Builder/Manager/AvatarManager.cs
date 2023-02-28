using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Inspector;
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
        private readonly MutableManager mutableManager;

        public AvatarManager(
            GameObject avatarObject,
            string tmpDir,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            Func<int> currentMenuSortPosition,
            MutableManager mutableManager
        ) {
            this.avatarObject = avatarObject;
            this.avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            this.tmpDir = tmpDir;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.currentMenuSortPosition = currentMenuSortPosition;
            this.mutableManager = mutableManager;
        }

        private MenuManager _menu;
        public MenuManager GetMenu() {
            if (_menu == null) {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                VRCFuryAssetDatabase.SaveAsset(menu, tmpDir, "VRCFury Menu for " + avatarObject.name);
                var initializing = true;
                _menu = new MenuManager(menu, tmpDir, () => initializing ? 0 : currentMenuSortPosition());

                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) _menu.MergeMenu(origMenu);
                
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
                var filename = "VRCFury " + type + " for " + avatarObject.name;
                AnimatorController ctrl;
                if (existingController != null) {
                    ctrl = mutableManager.CopyRecursive(existingController, filename);
                } else {
                    ctrl = new AnimatorController();
                    VRCFuryAssetDatabase.SaveAsset(ctrl, tmpDir, filename);
                }
                output = new ControllerManager(ctrl, GetParams, type, currentFeatureNumProvider, currentFeatureNameProvider, tmpDir, GetClipStorage());
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
                var filename = "VRCFury Params for " + avatarObject.name;
                VRCExpressionParameters prms;
                if (origParams != null) {
                    prms = mutableManager.CopyRecursive(origParams, filename);
                } else {
                    prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    prms.parameters = new VRCExpressionParameters.Parameter[]{};
                    VRCFuryAssetDatabase.SaveAsset(prms, tmpDir, filename);
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

            // Just for safety. These don't need to be here if we make sure everywhere else appropriately marks
            foreach (var c in _controllers.Values) {
                VRCFuryEditorUtils.MarkDirty(c.GetRaw());
            }
            if (_menu != null) VRCFuryEditorUtils.MarkDirty(_menu.GetRaw());
            if (_params != null) VRCFuryEditorUtils.MarkDirty(_params.GetRaw());
            if (_clipStorage != null) _clipStorage.MarkAllDirty();

            if (_params != null) {
                var maxParams = VRCExpressionParameters.MAX_PARAMETER_COST;
                if (maxParams > 9999) {
                    // Some versions of the VRChat SDK have a broken value for this
                    maxParams = 256;
                }
                if (_params.GetRaw().CalcTotalCost() > maxParams) {
                    throw new Exception(
                        "Avatar is out of space for parameters! Used "
                        + _params.GetRaw().CalcTotalCost() + "/" + maxParams
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
            VRCFuryEditorUtils.MarkDirty(avatar);
        }
    }
}
