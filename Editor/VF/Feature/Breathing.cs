using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model;
using VF.Model.StateAction;

namespace VF.Feature {

public class Breathing : BaseFeature<VF.Model.Feature.Breathing> {
    public override void Generate(VF.Model.Feature.Breathing config) {
        var clip = manager.NewClip("Breathing");
        var so = new SerializedObject(clip);
        so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = true;
        so.ApplyModifiedProperties();

        if (config.obj != null) {
            motions.Scale(clip, config.obj, ClipBuilder.FromSeconds(
                new Keyframe(0, config.scaleMin),
                new Keyframe(2.3f, config.scaleMax),
                new Keyframe(2.7f, config.scaleMax),
                new Keyframe(5, config.scaleMin)
            ));
        }
        if (!string.IsNullOrEmpty(config.blendshape)) {
            var breathingSkins = new List<SkinnedMeshRenderer>(GetAllSkins(avatarObject))
                .FindAll(skin => skin.sharedMesh.GetBlendShapeIndex(config.blendshape) != -1); 
            foreach (var skin in breathingSkins) {
                motions.BlendShape(clip, skin, config.blendshape, ClipBuilder.FromSeconds(
                    new Keyframe(0, 0),
                    new Keyframe(2.3f, 100),
                    new Keyframe(2.7f, 100),
                    new Keyframe(5, 0)
                ));
            }
        }

        var toggle = new VF.Model.Feature.Toggle {
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
