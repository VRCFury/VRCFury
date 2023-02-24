using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class FixWriteDefaultsEditor : FeatureBuilder<FixWriteDefaults> {
        public override string GetEditorTitle() {
            return "Fix Write Defaults";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(VRCFuryEditorUtils.Info(
                "This feature attempt to fix an avatar with a broken mix of Write Defaults."));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mode"), "Fix Mode"));
            container.Add(VRCFuryEditorUtils.Info(
                "Auto - Will force all states to on or off, whichever requires the fewest changes to the existing avatar\n" +
                "Force On - Forces all states to WD on\n" +
                "Force Off - Forces all states to WD off\n" +
                "Disabled - Don't try to fix anything and don't warn even if it looks broken"));
            return container;
        }
    }
}
