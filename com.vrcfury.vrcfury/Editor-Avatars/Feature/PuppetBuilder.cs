using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Utils.Controller;

namespace VF.Feature {

    internal class PuppetBuilder : FeatureBuilder<Puppet> {
        [VFAutowired] private readonly UniqueParamService uniqueParams;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        [FeatureBuilderAction]
        public void Apply() {
            var layerName = model.name;
            var layer = fx.NewLayer(layerName);
            
            var usesX = false;
            var usesY = false;
            foreach (var stop in model.stops) {
                if (stop.x != 0) usesX = true;
                if (stop.y != 0) usesY = true;
            }
            VFAParam Axis(string suffix, float defaultValue, bool used) {
                var name = model.name + suffix;
                if (!used) return fx.CreateUniqueFloat(name, def: defaultValue);
                var axis = fx.GetOrCreateFloat(uniqueParams.GetObjectScopedName(featureBaseObject, name), def: defaultValue);
                axis.MakeExpressionFloat(saved: model.saved);
                return axis;
            }
            var x = Axis("_x", model.defaultX, usesX);
            var y = Axis("_y", model.defaultY, usesY);
            
            var tree = VFBlendTree2D.CreateFreeformDirectional(model.name, x, y);
            tree.Add(new Vector2(0,0), clipFactory.GetEmptyClip());
            var i = 0;
            foreach (var stop in model.stops) {
                tree.Add(new Vector2(stop.x,stop.y), actionClipService.LoadState(model.name + "_" + i++, stop.state).EvaluateMotion(1));
            }
            layer.NewState("Blend").WithAnimation(tree);

            if (model.slider) {
                if (usesX) menu.NewMenuSlider(
                    model.name,
                    x,
                    icon: model.enableIcon ? model.icon.Get() : null
                );
            } else {
                menu.NewMenuPuppet(
                    model.name,
                    x: usesX ? x : null,
                    y: usesY ? y : null,
                    icon: model.enableIcon ? model.icon.Get() : null
                );
            }
        }
        
        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }

}
