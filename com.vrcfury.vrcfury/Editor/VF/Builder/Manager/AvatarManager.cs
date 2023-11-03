using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Feature;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class AvatarManager {
        private readonly VFGameObject avatarObject;
        private readonly VRCAvatarDescriptor avatar;
        private readonly string tmpDir;
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureNameProvider;
        private readonly Func<string> currentFeatureClipPrefixProvider;
        private readonly Func<int> currentMenuSortPosition;
        private readonly Func<VFGameObject> currentComponentObject;
        private readonly MutableManager mutableManager;

        public AvatarManager(
            GameObject avatarObject,
            string tmpDir,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureNameProvider,
            Func<string> currentFeatureClipPrefixProvider,
            Func<int> currentMenuSortPosition,
            Func<VFGameObject> currentComponentObject,
            MutableManager mutableManager
        ) {
            this.avatarObject = avatarObject;
            this.avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            this.tmpDir = tmpDir;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureNameProvider = currentFeatureNameProvider;
            this.currentFeatureClipPrefixProvider = currentFeatureClipPrefixProvider;
            this.currentMenuSortPosition = currentMenuSortPosition;
            this.currentComponentObject = currentComponentObject;
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
                var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);
                var filename = "VRCFury " + type + " for " + avatarObject.name;
                AnimatorController ctrl;
                if (existingController != null) {
                    ctrl = mutableManager.CopyRecursive(existingController, filename);
                    FullControllerBuilder.FixNullStateMachines(ctrl);
                } else {
                    ctrl = new AnimatorController();
                    VRCFuryAssetDatabase.SaveAsset(ctrl, tmpDir, filename);
                }
                output = new ControllerManager(
                    ctrl,
                    GetParams,
                    type,
                    currentFeatureNumProvider,
                    currentFeatureNameProvider,
                    currentFeatureClipPrefixProvider,
                    MakeUniqueParamName,
                    tmpDir,
                    treatAsManaged: isDefault
                );
                _controllers[type] = output;
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl);
            }
            return output;
        }
        public ControllerManager GetFx() {
            return GetController(VRCAvatarDescriptor.AnimLayerType.FX);
        }
        public IEnumerable<ControllerManager> GetAllUsedControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.controller != null)
                .Select(c => GetController(c.type))
                .ToArray();
        }
        public IEnumerable<Tuple<VRCAvatarDescriptor.AnimLayerType, VFController>> GetAllUsedControllersRaw() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.controller != null)
                .Select(c => Tuple.Create(c.type, c.controller));
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

        public string GetCurrentlyExecutingFeatureName() {
            return currentFeatureNameProvider();
        }

        public VFGameObject AvatarObject => avatarObject;
        public VFGameObject CurrentComponentObject => currentComponentObject();

        public bool IsParamUsed(string name) {
            if (GetParams().GetRaw().FindParameter(name) != null) return true;
            foreach (var c in GetAllUsedControllers()) {
                if (c.GetRaw().GetParam(name) != null) return true;
            }
            return false;
        }
        public string MakeUniqueParamName(string name) {
            name = "VF" + currentFeatureNumProvider() + "_" + name;

            int offset = 1;
            while (true) {
                var attempt = name + ((offset == 1) ? "" : offset+"");
                if (!IsParamUsed(attempt)) return attempt;
                offset++;
            }
        }
    }
}
