using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {

    internal class PuppetBuilder : FeatureBuilder<Puppet> {
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
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
            var x = fx.NewFloat(model.name + "_x", synced: usesX, saved: model.saved, def: model.defaultX);
            var y = fx.NewFloat(model.name + "_y", synced: usesY, saved: model.saved, def: model.defaultY);
            
            var tree = clipFactory.NewFreeformDirectional2D(model.name, x, y);
            tree.Add(new Vector2(0,0), clipFactory.GetEmptyClip());
            var i = 0;
            foreach (var stop in model.stops) {
                tree.Add(new Vector2(stop.x,stop.y), actionClipService.LoadState(model.name + "_" + i++, stop.state).GetLastFrame());
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
