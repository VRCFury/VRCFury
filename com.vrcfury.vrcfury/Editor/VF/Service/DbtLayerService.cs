using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    [VFPrototypeScope]
    internal class DbtLayerService {
        [VFAutowired] private readonly VFInjectorParent parent;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        public VFBlendTreeDirect Create(string name = null) {
            if (name == null) {
                name = $"DBT for {parent.parent.GetType().Name}";
            }
            var directLayer = fx.NewLayer(name);
            var tree = VFBlendTreeDirect.Create("DBT");
            directLayer.NewState("DBT").WithAnimation(tree);
            return tree;
        }

        public BlendtreeMath GetMath(VFBlendTreeDirect tree) {
            return new BlendtreeMath(fx, tree);
        }
    }
}
