using UnityEditor;
using UnityEngine.UIElements;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class ToesBuilder : FeatureBuilder<Toes> {
    public override void Apply() {
        var toes = new Puppet {
            name = "Toes"
        };
        if (StateExists(model.down)) toes.stops.Add(new Puppet.Stop(0,-1,model.down));
        if (StateExists(model.up)) toes.stops.Add(new Puppet.Stop(0,1,model.up));
        if (StateExists(model.splay)) {
            toes.stops.Add(new Puppet.Stop(-1,0,model.splay));
            toes.stops.Add(new Puppet.Stop(1,0,model.splay));
        }
        if (toes.stops.Count > 0) {
            addOtherFeature(toes);
        }
    }

    public override string GetEditorTitle() {
        return "Toes Puppet";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("down"), "Down"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("up"), "Up"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("splay"), "Splay"));
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
