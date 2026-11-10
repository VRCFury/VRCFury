using System.Collections.Generic;
using System.Linq;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class ObjectTreeService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly IsObjectEnabledService objectEnabled;
        [VFAutowired] private readonly AvatarBindingStateService bindingState;
        private readonly Dictionary<VFGameObject, (VFBlendTreeDirect tree, VFClip disabled)> trees = new();

        public VFBlendTreeDirect Get(VFGameObject obj) {
            return trees.GetOrCreate(obj, () => {
                var tree = VFBlendTreeDirect.Create("Calculations");
                var disabled = VFClip.Create(obj.name + " - Disabled");
                var enabled = BlendtreeMath.GreaterThan(objectEnabled.Get(obj), 0);
                var layer = controllers.GetFx().NewLayer(obj.name + " - Calculations");
                layer.NewState("Calculations").WithAnimation(enabled.create(tree, disabled));
                return (tree, disabled);
            }).tree;
        }

        [FeatureBuilderAction(FeatureOrder.BuildObjectTreeDefaults)]
        public void Apply() {
            foreach (var entry in trees.Values) {
                var bindings = new AnimatorIterator.Clips().From(entry.tree)
                    .SelectMany(c => c.GetAllCurves().Select(pair => (binding: pair.Item1, isFloat: pair.Item2.IsFloat)))
                    .Distinct();
                foreach (var (binding, isFloat) in bindings) {
                    if (binding.GetPropType() == EditorCurveBindingType.Aap) {
                        var output = binding.propertyName;
                        entry.disabled.SetAap(output, controllers.GetFx().TryGetParam(output, out var parameter)
                            ? parameter.GetDefault() : 0);
                    } else if (bindingState.Get(binding, isFloat, out var value)) {
                        entry.disabled.SetCurve(binding, value);
                    }
                }
            }
        }
    }
}
