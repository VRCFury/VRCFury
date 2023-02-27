using VF.Builder;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class CleanupBaseMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupBaseMasks)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();
                if (ctrl.layers[0].stateMachine.defaultState != null) {
                    // The base layer has stuff in it?
                    new VFAController(c.GetRaw(), c.GetType()).NewLayer("Base Mask", 0);
                    c.SetMask(0, c.GetMask(1));
                    c.SetMask(1, null);
                    c.SetWeight(1, 1);
                } else {
                    c.SetName(0, "Base Mask");
                }

                // We don't actually need to do this because unity always treats layer 0 as full weight,
                // but Gesture Manager shows 0 on the base mask even though it's not true, so let's just set
                // it to make it clear.
                c.SetWeight(0, 1);
                
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    c.SetMask(0, null);
                }
            }
        }
    }
}
