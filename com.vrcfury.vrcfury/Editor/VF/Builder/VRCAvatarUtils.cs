using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    internal static class VRCAvatarUtils {
        public class FoundController {
            public VRCAvatarDescriptor.AnimLayerType type;
            public bool isDefault;
            public RuntimeAnimatorController controller;
            public Action<RuntimeAnimatorController> set;
            public Action setToDefault;
        }
        
        private static IList<FoundController> GetAllControllers(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.CustomAnimLayer[] layers) {
            
            var output = new List<FoundController>();

            for (var i = 0; i < layers.Length; i++) {
                var layerNum = i;
                var layer = layers[layerNum];
                var type = layer.type;

                var isDefault = !avatar.customizeAnimationLayers || layer.isDefault;
                var controller = isDefault ? null : layer.animatorController;
                var foundController = new FoundController {
                    controller = controller,
                    type = type,
                    isDefault = isDefault
                };
                foundController.set = c => {
                    avatar.customizeAnimationLayers = true;
                    layer.isDefault = false;
                    layer.animatorController = c;
                    foundController.controller = c;
                    foundController.isDefault = false;
                    layers[layerNum] = layer;
                    VRCFuryEditorUtils.MarkDirty(avatar);
                };
                foundController.setToDefault = () => {
                    avatar.customizeAnimationLayers = true;
                    layer.isDefault = true;
                    layer.animatorController = null;
                    foundController.controller = null;
                    foundController.isDefault = true;
                    layers[layerNum] = layer;
                    VRCFuryEditorUtils.MarkDirty(avatar);
                };
                output.Add(foundController);
            }

            return output;
        }
        public static IList<FoundController> GetAllControllers(VRCAvatarDescriptor avatar) {
            return GetAllControllers(avatar, avatar.baseAnimationLayers)
                .Concat(GetAllControllers(avatar, avatar.specialAnimationLayers))
                .ToArray();
        }

        private static AnimatorController GetDefaultController(VRCAvatarDescriptor.AnimLayerType type) {
            string guid = null;
            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                // vrc_AvatarV3HandsLayer2
                guid = "5ecf8b95a27552840aef66909bdf588f";
            } else if (type == VRCAvatarDescriptor.AnimLayerType.Action) {
                // vrc_AvatarV3ActionLayer
                guid = "3e479eeb9db24704a828bffb15406520";
            } else if (type == VRCAvatarDescriptor.AnimLayerType.Base) {
                // vrc_AvatarV3LocomotionLayer
                guid = "4e4e1a372a526074884b7311d6fc686b";
            }
            if (guid == null) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null) {
                throw new Exception($"Failed to find default {type} controller");
            }
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) {
                throw new Exception($"Failed to load default {type} controller");
            }
            return c;
        }

        public static (bool,RuntimeAnimatorController) GetAvatarController(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type) {
            var matching = GetAllControllers(avatar).Where(layer => layer.type == type).ToArray();
            if (matching.Length == 0) {
                throw new VRCFBuilderException("Failed to find playable layer on avatar descriptor with type " + type);
            }
            if (matching.Length > 1) {
                throw new VRCFBuilderException("Found multiple playable layers on avatar descriptor with same type?? " + type);
            }

            var found = matching[0];
            if (found.isDefault) {
                var def = GetDefaultController(type);
                if (def != null) {
                    found.set(def);
                    return (true,def);
                }
            }

            return (false,found.controller);
        }
        
        public static void SetAvatarController(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type, RuntimeAnimatorController controller) {
            var setOne = false;
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.type == type) {
                    setOne = true;
                    layer.set(controller);
                }
            }

            if (!setOne) {
                throw new VRCFBuilderException(
                    "Failed to find " + type +
                    " layer on avatar. You may need to 'reset' the playable layers on the avatar descriptor, or your FBX may not be configured with a 'humanoid' rig.");
            }
        }

        public static VRCExpressionsMenu GetAvatarMenu(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionsMenu : null;
        }
        
        public static void SetAvatarMenu(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu) {
            avatar.customExpressions = true;
            avatar.expressionsMenu = menu;
            VRCFuryEditorUtils.MarkDirty(avatar);
        }

        public static VRCExpressionParameters GetAvatarParams(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionParameters : null;
        }
        
        public static void SetAvatarParams(VRCAvatarDescriptor avatar, VRCExpressionParameters prms) {
            avatar.customExpressions = true;
            avatar.expressionParameters = prms;
            VRCFuryEditorUtils.MarkDirty(avatar);
        }

        [CanBeNull]
        public static VFGameObject GuessAvatarObject(VFGameObject obj) {
            if (obj == null) return null;
            return obj.GetComponentInSelfOrParent<VRCAvatarDescriptor>()?.owner();
        }
        
        [CanBeNull]
        public static VFGameObject GuessAvatarObject(UnityEngine.Component c) {
            if (c == null) return null;
            return GuessAvatarObject(c.owner());
        }
    }
}
