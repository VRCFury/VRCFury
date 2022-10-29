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

        public AvatarManager(GameObject avatarObject, string tmpDir) {
            this.avatarObject = avatarObject;
            this.avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            this.tmpDir = tmpDir;
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

        private Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager> _controllers
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
                output = new ControllerManager(ctrl, GetParams(), type);
                _controllers[type] = output;
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl);
                if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                    var animator = avatarObject.GetComponent<Animator>();
                    if (animator != null) animator.runtimeAnimatorController = ctrl;
                }
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
                _clipStorage = new ClipStorageManager(tmpDir);
            }
            return _clipStorage;
        }

        public void Finish() {
            if (_menu != null) {
                MenuSplitter.SplitMenus(_menu.GetRaw());
            }

            // This is only here because a lot of the internal usages of these three forget to call setDirty themselves
            foreach (var c in _controllers.Values) {
                EditorUtility.SetDirty(c.GetRaw());
            }
            if (_menu != null) EditorUtility.SetDirty(_menu.GetRaw());
            if (_params != null) EditorUtility.SetDirty(_params.GetRaw());
            if (_clipStorage != null) _clipStorage.Finish();

            if (_params.GetRaw().CalcTotalCost() > VRCExpressionParameters.MAX_PARAMETER_COST) {
                throw new Exception(
                    "Avatar is out of space for parameters! Used "
                    + _params.GetRaw().CalcTotalCost() + "/" + VRCExpressionParameters.MAX_PARAMETER_COST
                    + ". Delete some params from your avatar's param file, or disable some VRCFury features.");
            }
        }

        public static AvatarMask GetBaseMask(AnimatorController ctrl) {
            return ctrl.layers[0]?.avatarMask;
        }
        public static void SetBaseMask(AnimatorController ctrl, AvatarMask mask) {
            var layers = ctrl.layers;
            layers[0].avatarMask = mask;
            ctrl.layers = layers;
        }
    }
}
