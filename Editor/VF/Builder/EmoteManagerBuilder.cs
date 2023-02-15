using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using Object = UnityEngine.Object;
using EmoteManager = VF.Model.Feature.EmoteManager;

namespace VF.Builder {

/**
 * Manages emotes via the action layer and VRCEmote
 */
public class EmoteManagerBuilder : FeatureBuilder<EmoteManager> {

    [FeatureBuilderAction]
    public void Apply() { }

    public override string GetEditorTitle() {
        return "Emote Manager";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("standingState"), "Standing State"));

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("sittingState"), "Sitting State:"));

        content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("afkState"), "AFK State:"));

        content.Add(VRCFuryEmoteEditor.render(prop.FindPropertyRelative("standingEmotes"), "Standing Emotes"));
        
        return content;
    }


    

    
    
    
}

}
