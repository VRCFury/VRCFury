using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Service {
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
    [VFService]
    internal class AnimatorHolderService {
        
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly VFGameObject avatarObject;

        private class SavedAnimator {
            public RuntimeAnimatorController controller;
            public VFController clone;
            public Avatar avatar;
            public bool applyRootMotion;
            public AnimatorUpdateMode updateMode;
            public AnimatorCullingMode cullingMode;
            public bool enabled;
        }

        private readonly Dictionary<VFGameObject, SavedAnimator> savedAnimators = new Dictionary<VFGameObject, SavedAnimator>();

        [FeatureBuilderAction(FeatureOrder.ResetAnimatorBefore)]
        public void ApplyBefore() {
            
            foreach (var animator in avatarObject.GetComponentsInSelfAndChildren<Animator>()) {
                var owner = animator.owner();

                if (owner != avatarObject && owner.GetComponents<VRCFury>().Any()) {
                    // This is a junk animator. Common for clothing prefabs, where the artist left an Animator
                    // on the asset to make it easier to record VRCFury animations. Just delete it.
                    Object.DestroyImmediate(animator);
                    continue;
                }

                savedAnimators.Add(animator.owner(), new SavedAnimator {
                    controller = animator.runtimeAnimatorController,
                    avatar = animator.avatar,
                    applyRootMotion = animator.applyRootMotion,
                    updateMode = animator.updateMode,
                    cullingMode = animator.cullingMode,
                    enabled = animator.enabled,
                });
                // In unity 2022, calling this when the animator hasn't called Update recently (meaning outside of play mode,
                // just entered play mode, object not enabled, etc) can make it write defaults that are NOT the proper resting state.
                // However, we probably don't even need this anymore since we initialize before the Animator would ever run now.
                // animator.WriteDefaultValues();
                Object.DestroyImmediate(animator);
            }
        }

        [FeatureBuilderAction(FeatureOrder.ResetAnimatorAfter)]
        public void ApplyAfter() {
            foreach (var pair in savedAnimators) {
                var obj = pair.Key;
                if (obj == null) continue;
                var saved = pair.Value;

                var animator = obj.AddComponent<Animator>();
                animator.applyRootMotion = saved.applyRootMotion;
                animator.updateMode = saved.updateMode;
                animator.cullingMode = saved.cullingMode;
                animator.avatar = saved.avatar;
                if (obj == avatarObject) {
                    if (saved.controller != null) {
                        animator.runtimeAnimatorController = fx.GetRaw();
                    }
                } else {
                    animator.runtimeAnimatorController = saved.controller;
                    animator.enabled = saved.enabled;
                }
            }
        }

        public IList<(VFGameObject owner, VFController controller)> GetSubControllers() {
            var output = new List<(VFGameObject, VFController)>();
            foreach (var pair in savedAnimators) {
                var owner = pair.Key;
                var saved = pair.Value;
                if (owner == avatarObject) continue;
                if (saved.controller == null) continue;
                if (saved.clone == null) {
                    saved.clone = VFController.CopyAndLoadController(saved.controller, VRCAvatarDescriptor.AnimLayerType.Base);
                    saved.controller = saved.clone?.GetRaw();
                }
                if (saved.clone == null) continue;
                output.Add((owner, saved.clone));
            }
            return output;
        }
    }
}
