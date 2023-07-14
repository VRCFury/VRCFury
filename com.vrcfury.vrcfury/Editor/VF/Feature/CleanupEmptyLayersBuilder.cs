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

                    var hasNonEmptyClip = new AnimatorIterator.Clips().From(layer)
                        .Any(clip => !ClipBuilder.IsEmptyMotion(clip));
                    var hasBehaviour = new AnimatorIterator.Behaviours().From(layer)
                        .Any();

                    if (!hasNonEmptyClip && !hasBehaviour) {
                        c.RemoveLayer(layer);
                    }
                }
            }
        }
    }
}
