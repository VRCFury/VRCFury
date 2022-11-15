using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class MakeWriteDefaultsOffBuilder : FeatureBuilder<MakeWriteDefaultsOff2> {
        [FeatureBuilderAction]
        public void DoStuff() {
            // The action of this model actually happens inside FixWriteDefaultsBuilder (which always runs on all builds)
        }

        public override string GetEditorTitle() {
            return "Make Write Defaults Off";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically make your avatar 'Write Defaults Off'."));
            return container;
        }
    }
}
