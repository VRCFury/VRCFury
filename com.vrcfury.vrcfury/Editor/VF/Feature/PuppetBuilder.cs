using UnityEditor.Animations;
using UnityEngine;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {

public class PuppetBuilder : FeatureBuilder<Puppet> {
    [FeatureBuilderAction]
    public void Apply() {
        var fx = GetFx();
        var layerName = model.name;
        var layer = fx.NewLayer(layerName);
        var tree = fx.NewBlendTree(model.name);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.AddChild(fx.GetEmptyClip(), new Vector2(0,0));
        var i = 0;
        var usesX = false;
        var usesY = false;
        foreach (var stop in model.stops) {
            if (stop.x != 0) usesX = true;
            if (stop.y != 0) usesY = true;
            tree.AddChild(LoadState(model.name + "_" + i++, stop.state), new Vector2(stop.x,stop.y));
        }
        var on = layer.NewState("Blend").WithAnimation(tree);

        var x = fx.NewFloat(model.name + "_x", synced: usesX, saved: model.saved, def: model.defaultX);
        tree.blendParameter = x.Name();
        var y = fx.NewFloat(model.name + "_y", synced: usesY, saved: model.saved, def: model.defaultY);
        tree.blendParameterY = y.Name();
        if (model.slider) {
            if (usesX) manager.GetMenu().NewMenuSlider(
                model.name,
                x,
                icon: model.enableIcon ? model.icon : null
            );
        } else {
            manager.GetMenu().NewMenuPuppet(
                model.name,
                x: usesX ? x : null,
                y: usesY ? y : null,
                icon: model.enableIcon ? model.icon : null
            );
        }
    }
}

}
