using UnityEngine;
using VRCF.Builder;

namespace VRCF.Feature {

public class AvatarScale : BaseFeature {
    public void Generate(Model.Feature.AvatarScale config) {
        var paramScale = manager.NewFloat("Scale", synced: true, def: 0.5f);
        manager.NewMenuSlider("Scale", paramScale);
        var scaleClip = manager.NewClip("Scale");
        var baseScale = avatarObject.transform.localScale.x;
        motions.Scale(scaleClip, avatarObject, VRCFuryClipUtils.FromFrames(
            new Keyframe(0, baseScale * 0.1f),
            new Keyframe(2, baseScale * 1),
            new Keyframe(3, baseScale * 2),
            new Keyframe(4, baseScale * 10)
        ));

        var layer = manager.NewLayer("Scale");
        var main = layer.NewState("Scale").WithAnimation(scaleClip).MotionTime(paramScale);
    }

    public override string GetEditorTitle() {
        return "Avatar Scale Slider";
    }
}

}
