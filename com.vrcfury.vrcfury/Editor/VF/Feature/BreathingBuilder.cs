using System;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class BreathingBuilder : FeatureBuilder<Breathing> {
    [VFAutowired] private readonly ActionClipService actionClipService;
    
    [FeatureBuilderAction]
    public void Apply() {

        var inClip = actionClipService.LoadState("breatheIn", model.inState);
        var outClip = actionClipService.LoadState("breatheOut", model.outState);

        var fx = GetFx();
        var clip = fx.NewClip("Breathing");
        clip.SetLooping(true);
        clipBuilder.MergeSingleFrameClips(clip,
            Tuple.Create(0f, outClip),
            Tuple.Create(2.5f, inClip),
            Tuple.Create(5f, outClip)
        );

        var toggle = new Toggle {
            name = "Breathing",
            defaultOn = true,
            state = new State {
                actions = { new AnimationClipAction { clip = clip } }
            }
        };
        addOtherFeature(toggle);
    }

    public override string GetEditorTitle() {
        return "Breathing Controller";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryEditorUtils.Info("This feature will add a breathing animation to your avatar, toggleable in menu. Only one state is required."));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("inState"), "Breathe-In"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("outState"), "Breathe-Out"));
        return content;
    }
    
    public override bool AvailableOnRootOnly() {
        return true;
    }
}

}
