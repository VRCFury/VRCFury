using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {

public class AvatarScaleBuilder : FeatureBuilder<AvatarScale> {
    [FeatureBuilderAction]
    public void Apply() {
        var paramScale = controller.NewFloat("Scale", synced: true, def: 0.5f, saved: true);
        var scaleClip = controller.NewClip("Scale");
        var baseScale = avatarObject.transform.localScale.x;
        motions.Scale(scaleClip, avatarObject, ClipBuilder.FromFrames(
            new Keyframe(0, baseScale * 0.1f),
            new Keyframe(2, baseScale * 1),
            new Keyframe(3, baseScale * 2),
            new Keyframe(4, baseScale * 10)
        ));

        var layer = controller.NewLayer("Scale");
        var main = layer.NewState("Scale").WithAnimation(scaleClip).MotionTime(paramScale);
        
        menu.NewMenuSlider("Scale/Adjust", paramScale);
        menu.NewMenuToggle("Scale/40%", paramScale, 0.20f);
        menu.NewMenuToggle("Scale/60%", paramScale, 0.27f);
        menu.NewMenuToggle("Scale/80%", paramScale, 0.35f);
        menu.NewMenuToggle("Scale/100%", paramScale, 0.50f);
        menu.NewMenuToggle("Scale/125%", paramScale, 0.58f);
        menu.NewMenuToggle("Scale/150%", paramScale, 0.62f);
        menu.NewMenuToggle("Scale/200%", paramScale, 0.75f);
        
    }

    public override string GetEditorTitle() {
        return "Avatar Scale Slider";
    }
    
    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(new Label() {
            text = "This feature will add a slider to your menu which will adjust your avatar's size." +
                   " NOTE: You need to change your avatar and change back for viewpoint to recalculate.",
            style = {
                whiteSpace = WhiteSpace.Normal
            }
        });
        content.Add(new PropertyField(prop.FindPropertyRelative("submenu"), "Folder name in menu"));
        return content;
    }

    public override bool AvailableOnProps() {
        return false;
    }
}

}
