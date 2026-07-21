using System.Linq;
using UnityEditor.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("Descriptor Debugger")]
    internal class DescriptorDebugBuilder : FeatureBuilder<DescriptorDebug> {

        [FeatureEditor]
        public static VisualElement Editor(VFGameObject avatarObject) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Info(
                "This component displays VRCFury's useful debug info about the controllers on your avatar's base avatar descriptor." +
                    " This component has no impact on the avatar, and changes nothing about the upload. Any detected warnings will be shown below."
                ));
            
            if (avatarObject != null) {
                var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatar != null) {
                    var controllers = VRCAvatarUtils.GetAllControllers(avatar)
                        .Select(c => c.controller as AnimatorController)
                        .NotNull()
                        .ToList();
                    foreach (var warning in VrcfAnimationDebugInfo.BuildDebugInfo(controllers, avatarObject)) {
                        output.Add(warning);
                    }
                }
            }

            return output;
        }
    }
}
