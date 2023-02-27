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

                    var remove = false;
                    if (layer.defaultState == null) {
                        remove = true;
                    } else if (
                        layer.stateMachines.Length == 0
                        && layer.states.Length == 1
                        && layer.states[0].state.behaviours.Length == 0
                        && ClipBuilder.IsEmptyMotion(layer.states[0].state.motion)
                    ) {
                        remove = true;
                    }

                    if (remove) {
                        c.RemoveLayer(layer);
                    }
                }
            }
        }
    }
}
