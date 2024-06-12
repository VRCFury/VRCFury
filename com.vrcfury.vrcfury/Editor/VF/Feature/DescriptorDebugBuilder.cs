using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    internal class DescriptorDebugBuilder : FeatureBuilder<DescriptorDebug> {
        public override string GetEditorTitle() {
            return "Descriptor Debugger";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Info(
                "This component displays VRCFury's useful debug info about the controllers on your avatar's base avatar descriptor." +
                    " This component has no impact on the avatar, and changes nothing about the upload. Any detected warnings will be shown below."
                ));
            var debugInfo = new VisualElement();
            output.Add(debugInfo);

            void Update() {
                debugInfo.Clear();
                if (avatarObject == null) return;
                var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatar == null) return;

                var controllers = VRCAvatarUtils.GetAllControllers(avatar)
                    .Select(c => c.controller as AnimatorController)
                    .NotNull()
                    .ToList();
                var warnings = VrcfAnimationDebugInfo.BuildDebugInfo(controllers, avatarObject, avatarObject);
                foreach (var w in warnings) {
                    debugInfo.Add(w);
                }
            }
            Update();
            debugInfo.schedule.Execute(Update).Every(1000);

            return output;
        }
    }
}