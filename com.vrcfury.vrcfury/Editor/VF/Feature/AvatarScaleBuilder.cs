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

    public override string GetEditorTitle() {
        return "Avatar Scale";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        return VRCFuryEditorUtils.Error(
            "This Avatar Scale feature is no longer available as scaling is now built into VRChat."
            );
    }

    public override bool AvailableOnRootOnly() {
        return true;
    }
    
    public override bool ShowInMenu() {
        return false;
    }
}

}
