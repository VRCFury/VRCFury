using System.Linq;
using VF.Feature.Base;

namespace VF.Feature {
    public class CleanupEmptyLayersBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupEmptyLayers)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var (layer, i) in c.GetLayers().Select((l,i) => (l,i))) {
                    if (layer.defaultState == null && i != 0) {
                        c.RemoveLayer(layer);
                    }
                }
            }
        }
    }
}
