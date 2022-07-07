using UnityEditor;
using UnityEngine.UIElements;
using VF.Inspector;

namespace VF.Feature {

public class TalkGlow : FeatureBuilder<VF.Model.Feature.Talking> {
    public override void Apply() {
        var layer = manager.NewLayer("Talk Glow");
        var clip = LoadState("TalkGlow", model.state);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);

        off.TransitionsTo(on).When(Viseme().IsGreaterThan(9));
        on.TransitionsTo(off).When(Viseme().IsLessThan(10));
    }

    public override string GetEditorTitle() {
        return "When-Talking State";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state")));
        return content;
    }
}

}
