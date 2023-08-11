using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using VF.Builder.Exceptions;
using VF.Inspector;
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

                AnimatorController GetDefaultController() {
                    string guid = null;
                    if (type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                        // vrc_AvatarV3HandsLayer2
                        guid = "5ecf8b95a27552840aef66909bdf588f";
                    } else if (type == VRCAvatarDescriptor.AnimLayerType.Action) {
                        // vrc_AvatarV3ActionLayer
                        guid = "3e479eeb9db24704a828bffb15406520";
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
                var controller = (avatar.customizeAnimationLayers && !layer.isDefault)
                    ? layer.animatorController as AnimatorController
                    : GetDefaultController();
                Action<AnimatorController> Set = c => {
                    avatar.customizeAnimationLayers = true;
                    layer.isDefault = c == null;
                    layer.animatorController = c;
                    layers[layerNum] = layer;
                    VRCFuryEditorUtils.MarkDirty(avatar);
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
            AnimatorController foundLayer = null;
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.Item3 == type && layer.Item1 != null) {
                    if (foundLayer != null && foundLayer != layer.Item1) {
                        throw new VRCFBuilderException(
                            "Avatar contains multiple expression layers of type " + type +
                            " with different animators for each!" +
                            " This is a VRChat bug. You may need to 'reset' the playable layers on the avatar descriptor.");
                    }
                    foundLayer = layer.Item1;
                }
            }
            return foundLayer;
        }
        
        public static void SetAvatarController(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType type, AnimatorController controller) {
            var setOne = false;
            foreach (var layer in GetAllControllers(avatar)) {
                if (layer.Item3 == type) {
                    setOne = true;
                    layer.Item2.Invoke(controller);
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
    }
}
