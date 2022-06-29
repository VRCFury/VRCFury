using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using VRCF.Inspector;

namespace VRCF.Feature {

public class Talking : BaseFeature {
    public void Generate(VRCF.Model.Feature.Talking config) {
        var layer = manager.NewLayer("Talk Glow");
        var clip = loadClip("TalkGlow", config.state);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);

        off.TransitionsTo(on).When(Viseme().IsGreaterThan(9));
        on.TransitionsTo(off).When(Viseme().IsLessThan(10));
    }

    public override string GetEditorTitle() {
        return "When-Talking State";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state")));
        return content;
    }
}

}
