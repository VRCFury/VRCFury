using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;

namespace VF.Feature {
    /**
     * Removes Animator components from objects that have VRCFury components. This is common for clothing
     * prefabs, where the artist left an Animator on the asset to make it easier to record VRCFury animations.
     */
    public class RemoveJunkAnimatorsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.RemoveJunkAnimators)]
        public void Apply() {
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFury>()) {
                var animator = c.gameObject.GetComponent<Animator>();
                if (animator != null && c.gameObject != avatarObject)
                    Object.DestroyImmediate(animator);
            }
        }
    }
}
