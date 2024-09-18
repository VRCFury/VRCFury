using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {
    [FeatureTitle("Apply During Upload")]
    internal class ApplyDuringUploadBuilder : FeatureBuilder<ApplyDuringUpload> {
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly ActionClipService actionClipService;
        
        [FeatureBuilderAction(FeatureOrder.ApplyDuringUpload)]
        public void Apply() {
            var clip = actionClipService.LoadStateAdv("applyDuringUpload", model.action);
            restingState.ApplyClipToRestingState(clip.bakingClip);
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();

            content.Add(VRCFuryEditorUtils.Info(
                "The following actions will be applied and baked into to the avatar during the upload process." +
                " This is useful if you want to enforce a specific upload state of your prop, even if the user has messed with it in the editor."));
            
            content.Add(VRCFuryEditorUtils.Info(
                "Note: 'Turn On' toggles automatically turn off their objects during upload, and thus do not need to be included here."));

            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("action")));

            return content;
        }
    }
}
