using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class BreathingBuilder : FeatureBuilder<Breathing> {
    [FeatureBuilderAction]
    public void Apply() {
        var clip = controller.NewClip("Breathing");
        var so = new SerializedObject(clip);
        so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = true;
        so.ApplyModifiedProperties();

        if (model.obj != null) {
            motions.Scale(clip, model.obj, ClipBuilder.FromSeconds(
                new Keyframe(0, model.scaleMin),
                new Keyframe(2.3f, model.scaleMax),
                new Keyframe(2.7f, model.scaleMax),
                new Keyframe(5, model.scaleMin)
            ));
        }
        if (!string.IsNullOrEmpty(model.blendshape)) {
            var breathingSkins = new List<SkinnedMeshRenderer>(GetAllSkins(avatarObject))
                .FindAll(skin => skin.sharedMesh.GetBlendShapeIndex(model.blendshape) != -1); 
            foreach (var skin in breathingSkins) {
                motions.BlendShape(clip, skin, model.blendshape, ClipBuilder.FromSeconds(
                    new Keyframe(0, 0),
                    new Keyframe(2.3f, 100),
                    new Keyframe(2.7f, 100),
                    new Keyframe(5, 0)
                ));
            }
        }

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
        content.Add(new Label("Choose either blendshape, or object + min + max scale"));
        content.Add(new PropertyField(prop.FindPropertyRelative("blendshape"), "On Blendshape"));
        content.Add(new PropertyField(prop.FindPropertyRelative("obj"), "Scale Object"));
        content.Add(new PropertyField(prop.FindPropertyRelative("scaleMin"), "Scale Object Min"));
        content.Add(new PropertyField(prop.FindPropertyRelative("scaleMax"), "Scale Object Max"));
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
