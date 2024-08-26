using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    internal class DeleteDuringUploadBuilder : FeatureBuilder<DeleteDuringUpload> {
        public override string GetEditorTitle() {
            return "Delete During Upload";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();

            content.Add(VRCFuryEditorUtils.Info(
                "This entire object, and all children, will be deleted during upload. No other VRCFury components stored within will be processed."));

            return content;
        }
    }
}
