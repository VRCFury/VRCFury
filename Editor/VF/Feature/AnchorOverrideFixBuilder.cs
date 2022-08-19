using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {
    public class AnchorOverrideFixBuilder : FeatureBuilder<AnchorOverrideFix> {
        [FeatureBuilderAction((int)FeatureOrder.AnchorOverrideFix)]
        public void Apply() {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) return;
            var root = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!root) root = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!root) return;
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                skin.probeAnchor = root;
            }
            foreach (var skin in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                skin.probeAnchor = root;
            }
        }
        
        public override bool AvailableOnProps() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "Anchor Override Fix";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(new Label() {
                text = "This feature will set the anchor override for every mesh on your avatar to your chest. This will prevent different meshes from being lit differently on your body.",
                style = {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return content;
        }
    }
}
