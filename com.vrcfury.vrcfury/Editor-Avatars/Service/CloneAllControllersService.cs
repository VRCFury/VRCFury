using VF.Feature.Base;
using VF.Injector;

namespace VF.Service {
    /**
     * Modular Avatar seems to have some sort of issue where it doesn't fully serialize all of the clips it generates to disk.
     * This means that if we later cause AssetDatabase.Refresh to be called, the content of those clips
     * may be rolled back before we have a chance to make a clone of them.
     *
     * AssetDatabase.Refresh happens inside of poiyomi's ShaderOptimizer, which we call out to in some situations,
     * which can cause this issue.
     *
     * To avoid this, we cause all controllers to clone immediately here, rather than waiting for
     * later when they're used.
     */
    [VFService]
    internal class CloneAllControllersService {
        [VFAutowired] private readonly ControllersService controllers;

        [FeatureBuilderAction(FeatureOrder.CloneAllControllers)]
        public void Apply() {
            controllers.GetAllUsedControllers();
        }
    }
}
