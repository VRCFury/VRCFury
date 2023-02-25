using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Feature {
    /**
     * This builder fixes an extremely rare unity bug, where if there are two IDENTICAL armatures
     * on the avatar (containing all the exact same bone names and layout), the Animator on the avatar root
     * may suddenly decide that its root is the nested object (with the nested armature) instead of the
     * actual avatar root.
     *
     * This breaks animator.GetBoneTransform (because it returns bones from the child armature), and also breaks
     * animator.GetFloatValue (because it looks for properties starting from that nested child object).
     *
     * Our solution to this is to find the hips bone, then rename every OTHER bone on the avatar using that name.
     * This makes absolutely sure that there can be no other matching armatures
     * inside the avatar, because they cannot have a bone with the root bone name.
     */
    public class FixDuplicateArmatureBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixDuplicateArmature)]
        public void Apply() {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) return;

            var hips = VRCFArmatureUtils.FindBoneOnArmature(avatarObject, HumanBodyBones.Hips);
            var movedOne = false;
            if (!hips) return;
            var i = 0;
            var mover = allBuildersInRun.OfType<ObjectMoveBuilder>().First();
            foreach (var child in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (child.gameObject == hips) continue;
                if (child.gameObject.name != hips.name) continue;
                // Amogus follower uses a sub-animator to animate its avatar, and (currently) we don't rewrite sub-animator
                // clips, so just skip these for now.
                if (AnimationUtility.CalculateTransformPath(child, avatarObject.transform).Contains("Follower")) {
                    continue;
                }                
                mover.Move(child.gameObject, newName: child.gameObject.name + "_vrcfdup" + (++i));
                movedOne = true;
            }

            if (movedOne) {
                animator.Rebind();
            }
        }
    }
}
