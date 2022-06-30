using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Inspector;

namespace VF.Feature {

public class Visemes : BaseFeature<VF.Model.Feature.Visemes> {
    public override void Generate(VF.Model.Feature.Visemes config) {
        if (config.oneAnim == null) return;

        var visemeFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config.oneAnim));
        var visemes = manager.NewLayer("Visemes");
        var VisemeParam = manager.NewInt("Viseme", usePrefix: false);
        Action<int, string> addViseme = (index, text) => {
            var animFileName = "Viseme-" + text;
            var clip = AssetDatabase.LoadMainAssetAtPath(visemeFolder + "/" + animFileName + ".anim") as AnimationClip;
            if (clip == null) throw new Exception("Missing animation for viseme " + animFileName);
            var state = visemes.NewState(text).WithAnimation(clip);
            if (text == "sil") state.Move(0, -8);
            state.TransitionsFromEntry().When(VisemeParam.IsEqualTo(index));
            state.TransitionsToExit().When(VisemeParam.IsNotEqualTo(index));
        };
        addViseme(0, "sil");
        addViseme(1, "PP");
        addViseme(2, "FF");
        addViseme(3, "TH");
        addViseme(4, "DD");
        addViseme(5, "kk");
        addViseme(6, "CH");
        addViseme(7, "SS");
        addViseme(8, "nn");
        addViseme(9, "RR");
        addViseme(10, "aa");
        addViseme(11, "E");
        addViseme(12, "I");
        addViseme(13, "O");
        addViseme(14, "U");
    }

    public override string GetEditorTitle() {
        return "Advanced Visemes";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("oneAnim")));
        return content;
    }
}

}
