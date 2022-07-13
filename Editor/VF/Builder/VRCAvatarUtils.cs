using System;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class VRCAvatarUtils {
        public static int GetAvatarFxLayerNumber(VRCAvatarDescriptor avatar) {
            if (!avatar.customizeAnimationLayers) return -1;
            for (var i = 0; i < avatar.baseAnimationLayers.Length; i++) {
                var layer = avatar.baseAnimationLayers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX) return i;
            }
            return -1;
        }

        public static AnimatorController GetAvatarFx(VRCAvatarDescriptor avatar) {
            if (!avatar.customizeAnimationLayers) return null;
            var layerNum = GetAvatarFxLayerNumber(avatar);
            if (layerNum < 0) return null;
            var layer = avatar.baseAnimationLayers[layerNum];
            if (layer.isDefault) return null;
            if (layer.animatorController == null) return null;
            return (AnimatorController)layer.animatorController;
        }
        
        public static void SetAvatarFx(VRCAvatarDescriptor avatar, AnimatorController fx) {
            var layerNum = GetAvatarFxLayerNumber(avatar);
            if (layerNum < 0)
                throw new Exception(
                    "Failed to find FX layer on avatar. You may need to 'reset' the expression layers on the avatar descriptor.");
            var layer = avatar.baseAnimationLayers[layerNum];
            avatar.customizeAnimationLayers = true;
            layer.isDefault = false;
            layer.animatorController = fx;
            avatar.baseAnimationLayers[layerNum] = layer;
        }

        public static VRCExpressionsMenu GetAvatarMenu(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionsMenu : null;
        }
        
        public static void SetAvatarMenu(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu) {
            avatar.customExpressions = true;
            avatar.expressionsMenu = menu;
        }

        public static VRCExpressionParameters GetAvatarParams(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionParameters : null;
        }
        
        public static void SetAvatarParams(VRCAvatarDescriptor avatar, VRCExpressionParameters prms) {
            avatar.customExpressions = true;
            avatar.expressionParameters = prms;
        }
    }
}
