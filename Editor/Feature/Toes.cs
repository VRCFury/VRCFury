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

public class Toes : BaseFeature {
    public void Generate(VRCF.Model.Feature.Toes config) {
        var toes = new VRCF.Model.Feature.Puppet();
        toes.name = "Toes";
        if (StateExists(config.down)) toes.stops.Add(new VRCFuryPropPuppetStop(0,-1,config.down));
        if (StateExists(config.up)) toes.stops.Add(new VRCFuryPropPuppetStop(0,1,config.up));
        if (StateExists(config.splay)) {
            toes.stops.Add(new VRCFuryPropPuppetStop(-1,0,config.splay));
            toes.stops.Add(new VRCFuryPropPuppetStop(1,0,config.splay));
        }
        if (toes.stops.Count > 0) {
            addOtherFeature(toes);
        }
    }

    public override string GetEditorTitle() {
        return "Toes Puppet";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("down"), "Down"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("up"), "Up"));
        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("splay"), "Splay"));
        return content;
    }
}

}
