using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {

public class VisemesBuilder : FeatureBuilder<Visemes> {
    private string[] visemeNames = {
        "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
    };
    
    [FeatureBuilderAction]
    public void Apply() {
        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
        if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.Default) {
            avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
        }

        var fx = GetFx();
        var visemes = fx.NewLayer("Visemes");
        var VisemeParam = fx.Viseme();
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
        content.Add(VRCFuryEditorUtils.Info(
            "This feature will allow you to use animations for your avatar's visemes."
        ));
        foreach (var name in visemeNames) {
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state_" + name), name, 50));
        }
        
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionTime"), "Transition Time (s)"));
        adv.Add(VRCFuryEditorUtils.WrappedLabel("-1 will use VRCFury recommended value"));
        content.Add(adv);
        
        return content;
    }
}

}
