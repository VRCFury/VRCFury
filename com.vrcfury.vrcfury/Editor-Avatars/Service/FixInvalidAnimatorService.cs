using System;
using UnityEditor;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {
    /**
     * VRChat doesn't load the avatar's radial menu unless you have an Animator and a valid
     * animator Avatar, even if it's a generic thing with no skeleton.
     * This fixes that issue, so even avatars without an animator at all will still properly
     * load their menu in game.
     */
    [VFService]
    internal class FixInvalidAnimatorService {
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.FixInvalidAnimator)]
        public void Apply() {
            var animator = avatarObject.GetComponent<Animator>();
            if (animator == null) {
                animator = avatarObject.AddComponent<Animator>();
            }

            if (animator.avatar != null && animator.avatar.isValid) return;

            var tempRoot = new GameObject(avatarObject.name);
            try {
                var avatar = AvatarBuilder.BuildGenericAvatar(tempRoot, "");
                if (avatar == null || !avatar.isValid) {
                    throw new Exception("Failed to build a valid root-only generic avatar");
                }
                avatar.name = "Root Only Generic Avatar";
                VrcfObjectFactory.Register(avatar);
                avatar.WorkLog("Created to provide a valid root-only generic avatar");
                animator.avatar = avatar;
            } finally {
                Object.DestroyImmediate(tempRoot);
            }
        }
    }
}
