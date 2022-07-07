using UnityEditor.Animations;
using UnityEngine;

namespace VF.Feature.Base {

public class Puppet : FeatureBuilder<VF.Model.Feature.Puppet> {
    public override void Apply() {
        var layerName = model.name;
        var layer = manager.NewLayer(layerName);
        var tree = manager.NewBlendTree(model.name);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.AddChild(manager.GetNoopClip(), new Vector2(0,0));
        var i = 0;
        var usesX = false;
        var usesY = false;
        foreach (var stop in model.stops) {
            if (stop.x != 0) usesX = true;
            if (stop.y != 0) usesY = true;
            tree.AddChild(LoadState(model.name + "_" + i++, stop.state), new Vector2(stop.x,stop.y));
        }
        var on = layer.NewState("Blend").WithAnimation(tree);

        var x = manager.NewFloat(model.name + "_x", synced: usesX);
        tree.blendParameter = x.Name();
        var y = manager.NewFloat(model.name + "_y", synced: usesY);
        tree.blendParameterY = y.Name();
        if (model.slider) {
            if (usesX) manager.NewMenuSlider(model.name, x);
        } else {
            manager.NewMenuPuppet(model.name, usesX ? x : null, usesY ? y : null);
        }
    }
}

}
