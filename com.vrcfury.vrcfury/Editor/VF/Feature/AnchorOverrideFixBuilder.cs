using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class AnchorOverrideFixBuilder : FeatureBuilder<AnchorOverrideFix2> {
        [FeatureBuilderAction(FeatureOrder.AnchorOverrideFix)]
        public void Apply() {
            GameObject root;
            try {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Chest);
            } catch (Exception) {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Hips);
            }
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                skin.probeAnchor = root.transform;
            }
        }
        
        public override bool AvailableOnRootOnly() {
            return true;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
        
        public override string GetEditorTitle() {
            return "Anchor Override Fix";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will set the anchor override for every mesh on your avatar to your chest. " +
                "This will prevent different meshes from being lit differently on your body."));
            return content;
        }
    }
}
