using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    /**
     * If a playable layer is set to "None", it can break the WD handling of the other remaining layers.
     * The side-effects are rarely noticeable, because it seems to somehow only break the behaviour "between frames",
     * temporarily resetting the state of objects before restoring them back to the animated state. Usually you won't notice
     * this, but in the case of Audio Sources, it causes the playing audio clip to restart because technically the component
     * was disabled and then re-enabled again.
     */
    [VFService]
    internal class NoUnsetPlayableLayersBuilder {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.FixUnsetPlayableLayers)]
        public void Apply() {
            var avatar = manager.Avatar;
            foreach (var c in VRCAvatarUtils.GetAllControllers(avatar)) {
                if (!c.isDefault && c.controller == null) {
                    manager.GetController(c.type);
                }
            }
        }
    }
}
