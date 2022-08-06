using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class MakeWriteDefaultsOffBuilder : FeatureBuilder<MakeWriteDefaultsOff> {
        [FeatureBuilderAction]
        public void DoStuff() {
        }

        public override string GetEditorTitle() {
            return "Make Write Defaults Off";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(VRCFuryEditorUtils.WrappedLabel("This feature will automatically make your avatar 'Write Defaults Off' (only during upload)"));
            return container;
        }
    }
}
