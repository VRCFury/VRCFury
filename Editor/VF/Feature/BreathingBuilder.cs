using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class BreathingBuilder : FeatureBuilder<Breathing> {
    [FeatureBuilderAction]
    public void Apply() {

        var inClip = LoadState("breatheIn", model.inState);
        var outClip = LoadState("breatheOut", model.outState);
        
        var clip = controller.NewClip("Breathing");
        var so = new SerializedObject(clip);
        so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = true;
        so.ApplyModifiedProperties();
        motions.MergeSingleFrameClips(clip,
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
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("inState"), "Breathe-In"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("outState"), "Breathe-Out"));
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
