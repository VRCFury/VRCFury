using UnityEngine;
using VF.Feature.Base;

namespace VF.Feature {
    public class ResetAnimatorBuilder : FeatureBuilder {
        
        /**
         * If GestureManager (or someone else) started animating our avatar before the build, we need to undo their changes
         * to get the avatar back into the default position. Tell the animator to put things back the way they were,
         * then nuke and recreate it so it resets its internal state.
         */
        [FeatureBuilderAction(FeatureOrder.ResetAnimatorBefore)]
        public void ApplyBefore() {
            var animator = avatarObject.GetComponent<Animator>();
            if (animator) animator.WriteDefaultValues();
            ResetAnimator(avatarObject);
        }
        
        /**
         * If GestureManager (or someone else) changes the controller on the Animator after we build during this same frame,
         * it would reset all the child transforms back to how they were before we built. To "lock them in," we need to
         * reset the animator again.
         */
        [FeatureBuilderAction(FeatureOrder.ResetAnimatorAfter)]
        public void ApplyAfter() {
            ResetAnimator(avatarObject);
        }

        /**
         * This method is needed, because:
         * 1. If you clip.SampleAnimation on the avatar while it has a humanoid Avatar set on its Animator, it'll
         *    bake into motorcycle pose.
         * 2. If you change the avatar or controller on the Animator, the Animator will reset all transforms of all
         *    children objects back to the way they were at the start of the frame.
         * Only destroying the animator then recreating it seems to "reset" this "start of frame" state.
         */
        public static void WithoutAnimator(GameObject obj, System.Action func) {
            var animator = obj.GetComponent<Animator>();
            if (!animator) {
                func();
                return;
            }

            var controller = animator.runtimeAnimatorController;
            var avatar = animator.avatar;
            var applyRootMotion = animator.applyRootMotion;
            var updateMode = animator.updateMode;
            var cullingMode = animator.cullingMode;
            Object.DestroyImmediate(animator);
            animator = obj.AddComponent<Animator>();
            animator.applyRootMotion = applyRootMotion;
            animator.updateMode = updateMode;
            animator.cullingMode = cullingMode;
            func();
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
        }

        public static void ResetAnimator(GameObject obj) {
            WithoutAnimator(obj, () => { });
        }
    }
}
