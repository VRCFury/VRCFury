using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class AvatarScaleBuilder : FeatureBuilder<AvatarScale2> {
    [FeatureBuilderAction]
    public void Apply() {
    }
    
    public override VisualElement CreateEditor(SerializedProperty prop) {
        return VRCFuryEditorUtils.Error(
            "This Avatar Scale feature is no longer available due to VRChat sdk changes. " +
                   "Please see the VRCFury/Prefabs/ThatFatKidsStuff/README.md" +
                   " for instructions on an alternative avatar scale implementation."
            );
    }

    public override bool AvailableOnRootOnly() {
        return true;
    }
}

}
