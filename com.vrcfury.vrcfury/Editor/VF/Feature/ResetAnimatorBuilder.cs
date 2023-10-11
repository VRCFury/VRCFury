using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using Object = UnityEngine.Object;

namespace VF.Feature {
    /**
     * Disabling the Animator during the build has a LOT of benefits:
     * 1. If you clip.SampleAnimation on the avatar while it has a humanoid Avatar set on its Animator, it'll
     *    bake into motorcycle pose.
     * 2. If you change the avatar or controller on the Animator, the Animator will reset all transforms of all
     *    children objects back to the way they were at the start of the frame.
     * 3. If GestureManager (or someone else) started animating our avatar before the build, we need to undo their changes
     *    to get the avatar back into the default position (WriteDefaultValues)
     * 4. If GestureManager (or someone else) changes the controller on the Animator after we build during this same frame,
     *    it would reset all the child transforms back to how they were before we built. To "lock them in," we need to
     *    reset the animator.
     */
    public class ResetAnimatorBuilder : FeatureBuilder {

        private bool exists;
        private RuntimeAnimatorController controller;
        private Avatar avatar;
        private bool applyRootMotion;
        private AnimatorUpdateMode updateMode;
        private AnimatorCullingMode cullingMode;

        [FeatureBuilderAction(FeatureOrder.ResetAnimatorBefore)]
        public void ApplyBefore() {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) {
                exists = false;
                return;
            }

            exists = true;
            controller = animator.runtimeAnimatorController;
            avatar = animator.avatar;
            applyRootMotion = animator.applyRootMotion;
            updateMode = animator.updateMode;
            cullingMode = animator.cullingMode;
            animator.WriteDefaultValues();
            VRCFArmatureUtils.ClearCache();
            VRCFArmatureUtils.WarmupCache(avatarObject);
            Object.DestroyImmediate(animator);
        }

        [FeatureBuilderAction(FeatureOrder.ResetAnimatorAfter)]
        public void ApplyAfter() {
            if (!exists) return;
            var animator = avatarObject.AddComponent<Animator>();
            animator.applyRootMotion = applyRootMotion;
            animator.updateMode = updateMode;
            animator.cullingMode = cullingMode;
            animator.avatar = avatar;
            if (controller != null) {
                animator.runtimeAnimatorController = GetFx().GetRaw();
            }
        }
    }
}
