using System.Linq;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service.Compressor {
    [VFService]
    internal class CompressorLayerUtilsService {
        public void FixWd(VFLayer layer, VFAvatarController controller) {
            var wdoff = controller.GetLayers().SelectMany(l => l.allStates).Any(state => !state.writeDefaultValues);
            if (wdoff) {
                foreach (var state in layer.allStates) {
                    state.writeDefaultValues = false;
                }
            }
        }
    }
}
