using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {

public class VisemesBuilder : FeatureBuilder<Visemes> {
    [VFAutowired] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;
    [VFAutowired] private readonly ActionClipService actionClipService;

    private readonly string[] visemeNames = {
        "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
    };
    
    [FeatureBuilderAction]
    public void Apply() {
        var avatar = manager.Avatar;
        if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.Default) {
            avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
        }

        var fx = GetFx();
        var layer = fx.NewLayer("Visemes");
        var VisemeParam = fx.Viseme();
        void addViseme(int index, string text, State clipState) {
            var clip = actionClipService.LoadState(text, clipState);
            var state = layer.NewState(text).WithAnimation(clip);
            if (text == "sil") state.Move(0, -8);
            state.TransitionsFromEntry().When(VisemeParam.IsEqualTo(index));
            var transitionTime = model.transitionTime >= 0 ? model.transitionTime : 0.07f;
            state.TransitionsToExit().When(VisemeParam.IsNotEqualTo(index)).WithTransitionDurationSeconds(transitionTime);
        }

        for (var i = 0; i < visemeNames.Length; i++) {
            var name = visemeNames[i];
            addViseme(i, name, (State)model.GetType().GetField("state_" + name).GetValue(model));
        }

        var blocked = layer.NewState("Blocked");
        trackingConflictResolverBuilder.WhenCollected(() => {
            if (!layer.Exists()) return; // Deleted by empty layer builder
            var inhibitors =
                trackingConflictResolverBuilder.GetInhibitors(TrackingConflictResolverBuilder.TrackingMouth);
            if (inhibitors.Count > 0) {
                var blockedWhen = VFCondition.Any(inhibitors.Select(inhibitor => inhibitor.IsGreaterThan(0)));
                blocked.TransitionsFromAny().When(blockedWhen);
                blocked.TransitionsToExit().When(blockedWhen.Not());
            }
        });
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
            var row = new VisualElement().Row();
            row.Add(new Label(name).FlexBasis(30));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state_" + name)).FlexGrow(1));
            content.Add(row);
        }
        
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionTime"), "Transition Time (in seconds, -1 will use VRCFury recommended value)"));
        content.Add(adv);
        
        return content;
    }
}

}
