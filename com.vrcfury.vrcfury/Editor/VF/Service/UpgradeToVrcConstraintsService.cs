using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * This does two things.
     * 1. It conveniently upgrades all uploaded avatars to VRCConstraints if possible, which
     *    provides an in-game frametime benefit.
     * 2. It fixes "half-upgraded" prefabs, where they user has manually upgraded constraints
     *    on their avatar, but not upgraded the controller coming from a VRCFury prefab.
     *
     * This needs to run before any vrcfury steps start looking for "invalid bindings" since the
     * constraint animations may be pointing the wrong type of constraint before this upgrade
     * has happened.
     */
    [VFService]
    internal class UpgradeToVrcConstraintsService {
        [VFAutowired] private readonly ClipRewriteService clipRewriteService;
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction(FeatureOrder.UpgradeToVrcConstraints)]
        public void Apply() {
            // TODO: Remove this conditional entirely and always upgrade once VRCConstraints are usable in the game live branch
#if VRCSDK_UPGRADE_CONSTRAINTS
            Upgrade();
#endif
        }

        delegate bool TryGetSubstituteAnimationBinding(
            Type unityConstraintType,
            string unityConstraintPropertyName,
            out Type vrcConstraintType,
            out string vrcConstraintPropertyName,
            out bool isArrayProperty
        );

        delegate bool ConvertUnityConstraintsToVrChatConstraints(
            IConstraint[] unityConstraints,
            VRCAvatarDescriptor avatarDescriptor,
            bool convertReferencedAnimationClips
        );

        private void Upgrade() {
            var tryUpgradeBinding = ReflectionUtils.GetMatchingDelegate<TryGetSubstituteAnimationBinding>(
                typeof(AvatarDynamicsSetup),
                "TryGetSubstituteAnimationBinding"
            );
            var upgradeConstraintComponents = ReflectionUtils.GetMatchingDelegate<ConvertUnityConstraintsToVrChatConstraints>(
                typeof(AvatarDynamicsSetup),
                "ConvertUnityConstraintsToVrChatConstraints"
            );
            if (tryUpgradeBinding == null || upgradeConstraintComponents == null) {
                Debug.LogError("Failed to find constraint binding upgrade methods");
                return;
                //throw new Exception("Failed to find constraint binding upgrade methods");
            }

            clipRewriteService.RewriteAllClips(AnimationRewriter.RewriteBinding(binding => {
                if (typeof(IConstraint).IsAssignableFrom(binding.type) && tryUpgradeBinding(
                        binding.type,
                        binding.propertyName,
                        out var newType,
                        out var newPropertyName,
                        out var isArrayProperty
                )) {
                    binding.type = newType;
                    binding.propertyName = newPropertyName;
                }
                return binding;
            }));

            var avatarDescriptor = globals.avatarObject.GetComponent<VRCAvatarDescriptor>();
            var unityConstraints = globals.avatarObject.GetComponentsInSelfAndChildren<IConstraint>().ToArray();
            upgradeConstraintComponents(unityConstraints, avatarDescriptor, false);
        }
    }
}
