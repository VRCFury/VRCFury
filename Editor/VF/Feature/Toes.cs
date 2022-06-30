using UnityEditor;
using UnityEngine.UIElements;
using VF.Inspector;
using VF.Model;

namespace VF.Feature {

public class Toes : BaseFeature<VF.Model.Feature.Toes> {
    public override void Generate(VF.Model.Feature.Toes config) {
        var toes = new VF.Model.Feature.Puppet {
            name = "Toes"
        };
        if (StateExists(config.down)) toes.stops.Add(new VF.Model.Feature.Puppet.Stop(0,-1,config.down));
        if (StateExists(config.up)) toes.stops.Add(new VF.Model.Feature.Puppet.Stop(0,1,config.up));
        if (StateExists(config.splay)) {
            toes.stops.Add(new VF.Model.Feature.Puppet.Stop(-1,0,config.splay));
            toes.stops.Add(new VF.Model.Feature.Puppet.Stop(1,0,config.splay));
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
