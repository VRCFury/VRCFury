using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Controller transitions using the wrong parameter type will prevent the controller
     * from loading entirely. Let's just remove those transitions.
     */
    [VFService]
    internal class NoBadControllerParamsService {
        [VFAutowired] private readonly ControllersService controllers;
        
        [FeatureBuilderAction(FeatureOrder.UpgradeWrongParamTypes)]
        public void Apply() {
            foreach (var c in controllers.GetAllUsedControllers()) {
                foreach (var tree in new AnimatorIterator.Trees().From(c)) {
                    if (tree.blendType == BlendTreeType.Direct) {
                        tree.RewriteChildren(child => {
                            if (child.directBlendParameter == VFBlendTreeDirect.AlwaysOneParam) {
                                child.directBlendParameter = c.One();
                            }
                            return child;
                        });
                    }
                }
                c.UpgradeWrongParamTypes();
                c.RemoveWrongParamTypes();
            }
        }
    }
}
