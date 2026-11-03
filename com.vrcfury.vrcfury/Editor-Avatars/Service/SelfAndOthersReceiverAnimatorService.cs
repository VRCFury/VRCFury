using VF.Builder.Haptics;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService(autoRegister = false)]
    internal class SelfAndOthersReceiverAnimatorService : SelfAndOthersReceiverService.AnimatorSupport {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService trees;
        private VFBlendTreeDirect selectorTree;

        public VFAParam GetParameter(VRCContactReceiver receiver, string suggestedName) {
            var fx = controllers.GetFx();
            if (string.IsNullOrEmpty(receiver.parameter)) receiver.parameter = fx.CreateUniqueFloat(suggestedName);
            // Newly requested advanced flags may add outputs to an existing receiver.
            foreach (var name in ContactReceiverUtils.GetParameterNames(receiver)) {
                fx.GetOrCreateFloat(name);
            }
            return fx.GetOrCreateFloat(receiver.parameter);
        }

        public void AddSelector(VFMotion selector) {
            if (selectorTree == null) selectorTree = trees.Create("Self/others receiver selection");
            selectorTree.Add(selector);
        }
    }
}
