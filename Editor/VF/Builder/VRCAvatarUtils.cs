using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class VRCAvatarUtils {
        private static
            IEnumerable<Tuple<AnimatorController, Action<AnimatorController>, VRCAvatarDescriptor.AnimLayerType>>
            GetAllControllers(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.CustomAnimLayer[] layers) {
            
            var output =
                new List<Tuple<AnimatorController, Action<AnimatorController>, VRCAvatarDescriptor.AnimLayerType>>();

            for (var i = 0; i < layers.Length; i++) {
                var layerNum = i;
                var layer = layers[layerNum];
                var type = layer.type;
                var controller = (avatar.customizeAnimationLayers && !layer.isDefault)
                    ? layer.animatorController as AnimatorController
                    : null;
                Action<AnimatorController> Set = c => {
                    avatar.customizeAnimationLayers = true;
                    layer.isDefault = false;
                    layer.animatorController = c;
                    layers[layerNum] = layer;
                    EditorUtility.SetDirty(avatar);
                };
                output.Add(Tuple.Create(controller, Set, type));
            }

            return output;
        }
        public static IEnumerable<Tuple<AnimatorController, Action<AnimatorController>, VRCAvatarDescriptor.AnimLayerType>> GetAllControllers(VRCAvatarDescriptor avatar) {
            return Enumerable.Concat(
                GetAllControllers(avatar, avatar.baseAnimationLayers),
                GetAllControllers(avatar, avatar.specialAnimationLayers));
        }

        public static AnimatorController GetAvatarController(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type) {
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.Item3 == type && layer.Item1 != null) {
                    return layer.Item1;
                }
            }
            return null;
        }
        
        public static void SetAvatarController(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type, AnimatorController controller) {
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.Item3 == type && layer.Item1 != null) {
                    layer.Item2.Invoke(controller);
                    return;
                }
            }
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.Item3 == type) {
                    layer.Item2.Invoke(controller);
                    return;
                }
            }
            throw new VRCFBuilderException(
                "Failed to find " + type + " layer on avatar. You may need to 'reset' the expression layers on the avatar descriptor.");
        }

        public static VRCExpressionsMenu GetAvatarMenu(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionsMenu : null;
        }
        
        public static void SetAvatarMenu(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu) {
            avatar.customExpressions = true;
            avatar.expressionsMenu = menu;
            EditorUtility.SetDirty(avatar);
        }

        public static VRCExpressionParameters GetAvatarParams(VRCAvatarDescriptor avatar) {
            return avatar.customExpressions ? avatar.expressionParameters : null;
        }
        
        public static void SetAvatarParams(VRCAvatarDescriptor avatar, VRCExpressionParameters prms) {
            avatar.customExpressions = true;
            avatar.expressionParameters = prms;
            EditorUtility.SetDirty(avatar);
        }
    }
}
