using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class RemoveBlinkingBuilder : FeatureBuilder<RemoveBlinking> {
        [FeatureBuilderAction]
        public void Apply() {
            var avatar = manager.Avatar;
            avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
        }

        public override string GetEditorTitle() {
            return "Remove Built-in Blinking";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This feature will disable blinking in the avatar's descriptor (setting eyelid type to None)." +
                " This is useful if you are using a VRCFury blink controller on a base avatar, and want to disable their default blinking without messing" +
                " with the stock files.");
        }

        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
    }
}
