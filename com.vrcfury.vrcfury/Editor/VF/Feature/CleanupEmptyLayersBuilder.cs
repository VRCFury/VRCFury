using System.Linq;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Feature {
    public class CleanupEmptyLayersBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupEmptyLayers)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var (layer, i) in c.GetLayers().Select((l,i) => (l,i))) {
                    if (i == 0) continue;

                    var hasNonEmptyClip = false;
                    var hasBehaviour = false;
                    AnimatorIterator.ForEachClip(layer, clip => {
                        if (!ClipBuilder.IsEmptyMotion(clip)) hasNonEmptyClip = true;
                    });
                    AnimatorIterator.ForEachBehaviour(layer, b => {
                        hasBehaviour = true;
                    });

                    if (!hasNonEmptyClip && !hasBehaviour) {
                        c.RemoveLayer(layer);
                    }
                }
            }
        }
    }
}
