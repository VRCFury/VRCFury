using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Upgrades animator storage types from parameter usages,
     * rewrites conditions for those types, then repairs any remaining invalid references.
     */
    [VFService]
    internal class NoBadControllerParamsService {
        [VFAutowired] private readonly ControllersService controllers;
        
        [FeatureBuilderAction(FeatureOrder.UpgradeWrongParamTypes)]
        public void Apply() {
            foreach (var c in controllers.GetAllUsedControllers()) {
                foreach (var tree in new AnimatorIterator.Trees().From(c)) {
                    if (tree.blendType != BlendTreeType.Direct) {
                        continue;
                    }
                    tree.RewriteChildren(child => {
                        if (child.directBlendParameter == VFBlendTreeDirect.AlwaysOneParam) {
                            child.directBlendParameter = c.One();
                        }
                        return child;
                    });
                }
                c.UpgradeWrongParamTypes();
                c.RemoveWrongParamTypes();
            }
        }
    }
}
