using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Anchor Override Fix")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class AnchorOverrideFixBuilder : FeatureBuilder<AnchorOverrideFix2> {
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.AnchorOverrideFix)]
        public void Apply() {
            VFGameObject root;
            try {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Chest);
            } catch (Exception) {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Hips);
            }
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                skin.probeAnchor = root;
            }
        }
        
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will set the anchor override for every mesh on your avatar to your chest. " +
                "This will prevent different meshes from being lit differently on your body."));
            return content;
        }
    }
}
