using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;

namespace VF.Feature {
    [VFService]
    internal class MarkThingsAsDirtyJustInCaseBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.MarkThingsAsDirtyJustInCase)]
        public void Apply() {
            // Just for safety. These don't need to be here if we make sure everywhere else appropriately marks
            foreach (var c in manager.GetAllUsedControllers()) {
                VRCFuryEditorUtils.MarkDirty(c.GetRaw());
            }
            VRCFuryEditorUtils.MarkDirty(manager.GetMenu().GetRaw());
            VRCFuryEditorUtils.MarkDirty(manager.GetParams().GetRaw());
        }
    }
}
