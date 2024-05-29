using System.Collections.Immutable;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Service {
    /**
     * If a physbone on a humanoid bone is not marked as animated, it prevents that bone from animating EVER, even in stations or MMD or the default vrc controllers.
     * This happens even if it has multiple children and is set to ignore root mode.
     * This can cause an issue if a physbone is placed on the hip bone (for dresses or tails), which will break sitting in chairs.
     */
    [VFService]
    internal class FixAnimatedPhysbonesService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction]
        public void Apply() {
            var bones = VRCFArmatureUtils.GetAllBones(avatarObject).Values.ToImmutableHashSet();
            foreach (var physbone in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                var root = physbone.GetRootTransform();
                if (root == avatarObject || bones.Contains(root)) {
                    if (!physbone.isAnimated) {
                        Debug.LogWarning($"Marking physbone on {physbone.owner().GetPath(avatarObject, true)} as animated since it targets a humanoid bone");
                        physbone.isAnimated = true;
                    }
                }
            }
        }
    }
}
