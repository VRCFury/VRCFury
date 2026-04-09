using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal static class VRCAvatarDescriptorExtensions {

        public static void FixInvalidLayers(this VRCAvatarDescriptor avatar) {
            avatar.FixNullArrays();
            avatar.FixDoubleFx();
        }

        /**
         * The VRCSDK only creates the layer arrays if the editor was unfolded at least once in the editor.
         * In case it wasn't, we have to create the arrays ourselves.
         */
        private static void FixNullArrays(this VRCAvatarDescriptor avatar) {
            if (avatar.baseAnimationLayers == null) {
                if (avatar.GetComponent<Animator>()?.isHuman ?? false) {
                    avatar.baseAnimationLayers = new [] {
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Base, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Additive, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Gesture, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = true },
                    };
                } else {
                    avatar.baseAnimationLayers = new [] {
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Base, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action, isDefault = true },
                        new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = true },
                    };
                }
            }

            if (avatar.specialAnimationLayers == null) {
                avatar.specialAnimationLayers = new [] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Sitting, isDefault = true },
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.TPose, isDefault = true },
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.IKPose, isDefault = true },
                };
            }
        }

        /**
         * Old versions of the VRCSDK could create a corrupted layer array where Action was replaced by another copy of FX.
         * This fixes the issue.
         */
        private static void FixDoubleFx(this VRCAvatarDescriptor avatar) {
            var fxLayers = new List<RuntimeAnimatorController>();
            var fxLayerIds = new List<int>();
            var fxLayerDefault = new List<bool>();
            for (var i = 0; i < avatar.baseAnimationLayers.Length; i++) {
                var layer = avatar.baseAnimationLayers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX) {
                    fxLayers.Add(layer.isDefault ? null : layer.animatorController);
                    fxLayerIds.Add(i);
                    fxLayerDefault.Add(layer.isDefault);
                }
            }

            if (fxLayers.Count > 1) {
                var uniqueFx = fxLayers
                    .Where(l => l != null)
                    .Distinct()
                    .ToList();
                if (uniqueFx.Count > 1) {
                    throw new Exception(
                        "Avatar contains more than one unique FX playable layer." +
                        " Check the Avatar Descriptor, and remove the FX controller that shouldn't be there.");
                }
                var allDefault = fxLayerDefault.All(a => a);
                foreach (var id in fxLayerIds) {
                    var layer = avatar.baseAnimationLayers[id];
                    if (id == fxLayerIds[0]) {
                        layer.type = VRCAvatarDescriptor.AnimLayerType.Action;
                        layer.isDefault = true;
                    } else if (allDefault) {
                        layer.isDefault = true;
                        layer.animatorController = null;
                    } else if (id == fxLayerIds[1]) {
                        layer.isDefault = false;
                        layer.animatorController = uniqueFx.Count > 0 ? uniqueFx[0] : null;
                    } else {
                        layer.isDefault = false;
                        layer.animatorController = null;
                    }
                    avatar.baseAnimationLayers[id] = layer;
                }
            }
        }
    }
}
