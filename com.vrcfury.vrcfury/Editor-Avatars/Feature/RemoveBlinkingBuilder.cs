using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("Remove Built-in Blinking")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class RemoveBlinkingBuilder : FeatureBuilder<RemoveBlinking> {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;

        [FeatureBuilderAction]
        public void Apply() {
            avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return VRCFuryEditorUtils.Info(
                "This feature will disable blinking in the avatar's descriptor (setting eyelid type to None)." +
                " This is useful if you are using a VRCFury blink controller on a base avatar, and want to disable their default blinking without messing" +
                " with the stock files.");
        }
    }
}
