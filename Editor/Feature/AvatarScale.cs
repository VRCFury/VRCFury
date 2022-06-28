using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Feature {

public class AvatarScale : BaseFeature {
    public void Generate(VRCF.Model.Feature.AvatarScale config) {
        var paramScale = manager.NewFloat("Scale", synced: true, def: 0.5f);
        manager.NewMenuSlider("Scale", paramScale);
        var scaleClip = manager.NewClip("Scale");
        var baseScale = avatarObject.transform.localScale.x;
        motions.Scale(scaleClip, avatarObject, motions.FromFrames(
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
