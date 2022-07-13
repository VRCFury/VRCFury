using UnityEditor.Animations;
using UnityEngine;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {

public class PuppetBuilder : FeatureBuilder<Puppet> {
    [FeatureBuilderAction]
    public void Apply() {
        var layerName = model.name;
        var layer = controller.NewLayer(layerName);
        var tree = controller.NewBlendTree(model.name);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.AddChild(controller.GetNoopClip(), new Vector2(0,0));
        var i = 0;
        var usesX = false;
        var usesY = false;
        foreach (var stop in model.stops) {
            if (stop.x != 0) usesX = true;
            if (stop.y != 0) usesY = true;
            tree.AddChild(LoadState(model.name + "_" + i++, stop.state), new Vector2(stop.x,stop.y));
        }
        var on = layer.NewState("Blend").WithAnimation(tree);

        var x = controller.NewFloat(model.name + "_x", synced: usesX);
        tree.blendParameter = x.Name();
        var y = controller.NewFloat(model.name + "_y", synced: usesY);
        tree.blendParameterY = y.Name();
        if (model.slider) {
            if (usesX) menu.NewMenuSlider(model.name, x);
        } else {
            menu.NewMenuPuppet(model.name, usesX ? x : null, usesY ? y : null);
        }
    }
}

}
