using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * When controlling an AAP using a blend tree, the "default value" of the parameter will be included (at least partially)
     * in the calculated value UNLESS the weight of the inputs is >= 1. We can prevent it from being involved at all by animating the
     * value to 0 with weight 1.
     *
     * We CANNOT skip this even if the default value of the parameter is 0, because vrchat can cause the animator's parameter defaults
     * to change unexpectedly in situations such as leaving a station.
     *
     * In theory, we could skip the safety setter IF it's guaranteed that the weight will always be >= 1 in all other usages of the AAP
     * but this is complicated to keep track of for limited benefit.
     *
     * WARNING: If your aap is animated from a direct blendtree OUTSIDE of the main shared direct blendtree, you must set useWeightProtection to false
     * and ensure that you weight protect the variable in your own tree.
     */
    [VFService]
    internal class FixPartiallyWeightedAapsService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly FixWriteDefaultsService writeDefaultsService;
        
        [FeatureBuilderAction(FeatureOrder.FixPartiallyWeightedAaps)]
        public void Apply() {
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw()).Where(VFLayer.Created)) {
                if (state.motion is BlendTree tree) {
                    var aaps = new AnimatorIterator.Clips().From(tree)
                        .SelectMany(clip => clip.GetFloatBindings())
                        .Where(binding => binding.GetPropType() == EditorCurveBindingType.Aap)
                        .Select(binding => binding.propertyName)
                        .ToArray();
                    if (aaps.Any()) {
                        var wrapper = VFBlendTreeDirect.Create(tree.name + " (AAP Fixed)");
                        var zeroClip = clipFactory.NewClip("Set AAPs to 0");
                        wrapper.Add(zeroClip);
                        wrapper.Add(tree);
                        state.motion = wrapper;
                        
                        foreach (var aap in aaps) {
                            // Ensure every tree that animates an AAP has a 1-weight clip that sets it to zero
                            zeroClip.SetAap(aap, 0);
                            
                            // VRChat can break the "default value" of AAPs when using a station, so we need to make sure it's
                            // the default when it's not animated (usually 0)
                            var defaultValue = fx.GetRaw().GetParam(aap);
                            if (defaultValue != null) {
                                writeDefaultsService.GetDefaultClip().SetAap(aap, defaultValue.GetDefaultValueAsFloat());
                            }
                        }
                    }
                }
            }
        }
    }
}
