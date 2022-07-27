using System;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class VRCAvatarUtils {
        private static int GetAvatarLayerNumber(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type) {
            if (!avatar.customizeAnimationLayers) return -1;
            // Sometimes, broken avatar descriptors have multiple entries for the same type.
            // We'll do a first pass to see if there's one actually used (in case its not the first one during this bug)
            var seenCount = 0;
            var firstSeenIndex = -1;
            for (var i = 0; i < avatar.baseAnimationLayers.Length; i++) {
                var layer = avatar.baseAnimationLayers[i];
                if (layer.type == type) {
                    seenCount++;
                    if (firstSeenIndex < 0) firstSeenIndex = i;
                    if (!layer.isDefault && layer.animatorController != null) return i;
                }
            }
            return firstSeenIndex;
        }

        public static AnimatorController GetAvatarFx(VRCAvatarDescriptor avatar) {
            if (!avatar.customizeAnimationLayers) return null;
            var layerNum = GetAvatarLayerNumber(avatar, VRCAvatarDescriptor.AnimLayerType.FX);
            if (layerNum < 0) return null;
            var layer = avatar.baseAnimationLayers[layerNum];
            if (layer.isDefault) return null;
            if (layer.animatorController == null) return null;
            return (AnimatorController)layer.animatorController;
        }
        
        public static void SetAvatarFx(VRCAvatarDescriptor avatar, AnimatorController fx) {
            avatar.customizeAnimationLayers = true;
            var layerNum = GetAvatarLayerNumber(avatar, VRCAvatarDescriptor.AnimLayerType.FX);
            if (layerNum < 0)
                throw new Exception(
                    "Failed to find FX layer on avatar. You may need to 'reset' the expression layers on the avatar descriptor.");
            var layer = avatar.baseAnimationLayers[layerNum];
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
