using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;

namespace VF.Feature {

public class VisemesBuilder : FeatureBuilder<Visemes> {
    private string[] visemeNames = {
        "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
    };
    
    [FeatureBuilderAction]
    public void Apply() {
        var visemes = controller.NewLayer("Visemes");
        var VisemeParam = controller.NewInt("Viseme", usePrefix: false);
        void addViseme(int index, string text, State clipState) {
            var clip = LoadState(text, clipState);
            var state = visemes.NewState(text).WithAnimation(clip);
            if (text == "sil") state.Move(0, -8);
            state.TransitionsFromEntry().When(VisemeParam.IsEqualTo(index));
            var transitionTime = model.transitionTime >= 0 ? model.transitionTime : 0.07f;
            state.TransitionsToExit().When(VisemeParam.IsNotEqualTo(index)).WithTransitionDurationSeconds(transitionTime);
        }

        for (var i = 0; i < visemeNames.Length; i++) {
            var name = visemeNames[i];
            addViseme(i, name, (State)model.GetType().GetField("state_" + name).GetValue(model));
        }
    }

    public override string GetEditorTitle() {
        return "Advanced Visemes";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        foreach (var name in visemeNames) {
            content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state_" + name), name));
        }
        
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(new PropertyField(prop.FindPropertyRelative("transitionTime"), "Transition Time (s)"));
        adv.Add(new Label("-1 will use VRCFury recommended value"));
        content.Add(adv);
        
        return content;
    }
}

}
