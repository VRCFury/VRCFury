using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

    [FeatureTitle("Toes Puppet")]
    [FeatureRootOnly]
    internal class ToesBuilder : FeatureBuilder<Toes> {
        [FeatureBuilderAction]
        public void Apply() {
            var toes = new Puppet {
                name = "Toes"
            };
            toes.stops.Add(new Puppet.Stop(0,-1,model.down));
            toes.stops.Add(new Puppet.Stop(0,1,model.up));
            toes.stops.Add(new Puppet.Stop(-1,0,model.splay));
            toes.stops.Add(new Puppet.Stop(1,0,model.splay));
            if (toes.stops.Count > 0) {
                addOtherFeature(toes);
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("down"), "Down"));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("up"), "Up"));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("splay"), "Splay"));
            return content;
        }
    }

}
