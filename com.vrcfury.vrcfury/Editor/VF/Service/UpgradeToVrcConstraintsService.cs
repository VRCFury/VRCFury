using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Validation.Performance;

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
#if VRCSDK_HAS_VRCCONSTRAINTS
        [VFAutowired] private readonly ClipRewriteService clipRewriteService;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.UpgradeToVrcConstraints)]
        public void Apply() {
            Upgrade();
        }

        private void Upgrade() {

            HashSet<VFGameObject> limitToObjects = null;
            if (!AutoUpgradeConstraintsMenuItem.Get()) {
                limitToObjects = new HashSet<VFGameObject>();
                limitToObjects.UnionWith(clipRewriteService.GetAllClips()
                    .SelectMany(clip => clip.GetFloatBindings())
                    .Where(binding => typeof(IVRCConstraint).IsAssignableFrom(binding.type))
                    .Select(binding => avatarObject.Find(binding.path))
                    .NotNull());
                limitToObjects.UnionWith(avatarObject.GetSelfAndAllChildren()
                    .Where(obj => obj.GetComponent<IVRCConstraint>() != null));
            }

            clipRewriteService.RewriteAllClips(AnimationRewriter.RewriteBinding(binding => {
                if (!typeof(IConstraint).IsAssignableFrom(binding.type)) return binding;
                if (limitToObjects != null) {
                    var obj = avatarObject.Find(binding.path);
                    if (obj == null || !limitToObjects.Contains(obj)) return binding;
                }
                if (AvatarDynamicsSetup.TryGetSubstituteAnimationBinding(
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

            var avatarDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
            IConstraint[] unityConstraints;
            if (limitToObjects != null) {
                unityConstraints = limitToObjects.SelectMany(obj => obj.GetComponents<IConstraint>()).ToArray();
            } else {
                unityConstraints = avatarObject.GetComponentsInSelfAndChildren<IConstraint>().ToArray();
            }
            AvatarDynamicsSetup.DoConvertUnityConstraints(unityConstraints, avatarDescriptor, false);
        }
#endif
    }
}
