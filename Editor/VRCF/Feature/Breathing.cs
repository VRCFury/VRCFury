using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Builder;
using VRCF.Model;

namespace VRCF.Feature {

public class Breathing : BaseFeature {
    public void Generate(Model.Feature.Breathing config) {
        var clip = manager.NewClip("Breathing");
        var so = new SerializedObject(clip);
        so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = true;
        so.ApplyModifiedProperties();

        if (config.obj != null) {
            motions.Scale(clip, config.obj, VRCFuryClipUtils.FromSeconds(
                new Keyframe(0, config.scaleMin),
                new Keyframe(2.3f, config.scaleMax),
                new Keyframe(2.7f, config.scaleMax),
                new Keyframe(5, config.scaleMin)
            ));
        }
        if (!string.IsNullOrEmpty(config.blendshape)) {
            var breathingSkins = getAllSkins().FindAll(skin => skin.sharedMesh.GetBlendShapeIndex(config.blendshape) != -1); 
            foreach (var skin in breathingSkins) {
                motions.BlendShape(clip, skin, config.blendshape, VRCFuryClipUtils.FromSeconds(
                    new Keyframe(0, 0),
                    new Keyframe(2.3f, 100),
                    new Keyframe(2.7f, 100),
                    new Keyframe(5, 0)
                ));
            }
        }

        var toggle = new Model.Feature.Toggle();
        toggle.name = "Breathing";
        toggle.defaultOn = true;
        toggle.state = new VRCFuryState();
        toggle.state.clip = clip;
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
}

}
