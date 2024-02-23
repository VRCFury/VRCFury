using System.Linq;
using UnityEditor;
using UnityEngine;
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
    [VFAutowired] private readonly DirectBlendTreeService directTree;
    [VFAutowired] private readonly MathService math;
    [VFAutowired] private readonly SmoothingService smooth;

    private readonly string[] visemeNames = {
        "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
    };
    
    [FeatureBuilderAction]
    public void Apply() {
        var avatar = manager.Avatar;
        if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) {
            avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
        }
        
        var transitionTime = model.transitionTime >= 0 ? model.transitionTime : 0.07f;

        var fx = GetFx();
        var VisemeParam = fx.NewFloat("Viseme", usePrefix: false);
        var VolumeParam = fx.NewFloat("Voice", usePrefix: false);

        var voiceTree = math.MakeDirect("Advanced Visemes");
        var enabled = math.MakeAap("AdvancedVisemesEnabled", def: 1);
        var volumeTree = math.MakeDirect("Advanced Visemes");
        volumeTree.Add(VolumeParam, voiceTree);
        directTree.Add(enabled, volumeTree);

        void addViseme(int index, string text, State clipState) {
            var clip = actionClipService.LoadState(text, clipState);
            var intensityRaw = math.SetValueWithConditions(
                $"{text}Raw",
                (1, math.Equals(VisemeParam, index)),
                (0, null)
            );
            var intensitySmooth = smooth.Smooth($"{text}Smooth", intensityRaw, transitionTime, false);
            voiceTree.Add(intensitySmooth, clip);
        }

        for (var i = 0; i < visemeNames.Length; i++) {
            var name = visemeNames[i];
            var fieldName = "state_" + ((i == 0) ? "aa" : name);
            addViseme(i, name, (State)model.GetType().GetField(fieldName).GetValue(model));
        }

        trackingConflictResolverBuilder.WhenCollected(() => {
            var inhibitors =
                trackingConflictResolverBuilder.GetInhibitors(TrackingConflictResolverBuilder.TrackingMouth);

            var enabledWhen = math.True();
            foreach (var inhibitor in inhibitors) {
                enabledWhen = math.And(enabledWhen, math.LessThan(inhibitor, 0.5f));
            }

            directTree.Add(enabledWhen.create(
                math.MakeSetter(enabled, 1),
                new AnimationClip()
            ));
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
            if (name == "sil") continue;
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
