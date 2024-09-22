using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("HC Retarget")]
    internal class HeadChopHeadBuilder : FeatureBuilder<HeadChopHead> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly Func<VFGameObject> componentObject;

        [FeatureBuilderAction]
        public void Apply() {
#if VRCSDK_HAS_HEAD_CHOP
            var chop = componentObject().GetComponent<VRCHeadChop>();
            if (chop == null) {
                throw new Exception("Object does not contain a VRC Head Chop component");
            }
            if (chop.targetBones.Length != 1) {
                throw new Exception("VRC Head Chop component on this object does not contain exactly one target");
            }

            chop.targetBones[0].transform =
                VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Head);
#else
            throw new Exception("This version of the VRCSDK does not support head chop");
#endif
        }

        [FeatureEditor]
        public static VisualElement Builder(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This component forces a VRC head chop on the current object to point to the avatar's head bone." +
                " The head chop component MUST contain only one source, and that source will be overwritten with the avatar's head." +
                "\n\n" +
                "This is basically only useful for GogoLoco.");
        }
    }
}
