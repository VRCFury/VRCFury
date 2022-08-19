using UnityEngine;
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
    }
}
