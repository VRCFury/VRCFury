using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class TalkingBuilder : FeatureBuilder<Talking> {
    [FeatureBuilderAction]
    public void Apply() {
        var fx = GetFx();
        var layer = fx.NewLayer("Talk Glow");
        var clip = LoadState("TalkGlow", model.state);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);

        off.TransitionsTo(on).When(fx.Viseme().IsGreaterThan(9));
        on.TransitionsTo(off).When(fx.Viseme().IsLessThan(10));
    }

    public override string GetEditorTitle() {
        return "When-Talking State";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryEditorUtils.Info("This feature will activate the given animation whenever the avatar is talking."));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state")));
        return content;
    }
}

}
