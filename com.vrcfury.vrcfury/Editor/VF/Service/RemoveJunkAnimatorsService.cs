using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;

namespace VF.Service {
    /**
     * Removes Animator components from objects that have VRCFury components. This is common for clothing
     * prefabs, where the artist left an Animator on the asset to make it easier to record VRCFury animations.
     */
    [VFService]
    internal class RemoveJunkAnimatorsService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly AnimatorHolderService animators;

        [FeatureBuilderAction(FeatureOrder.RemoveJunkAnimators)]
        public void Apply() {
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFury>()) {
                animators.RemoveAnimator(c.owner());
            }
        }
    }
}
