using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("Fix Write Defaults")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class FixWriteDefaultsBuilder : FeatureBuilder<FixWriteDefaults> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var container = new VisualElement();
            container.Add(VRCFuryEditorUtils.Info(
                "This feature attempt to fix an avatar with a broken mix of Write Defaults."));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mode"), "Fix Mode"));
            container.Add(VRCFuryEditorUtils.Info(
                "Auto - Will force all states to on or off, whichever requires the fewest changes to the existing avatar\n" +
                "Force On - Forces all states to WD on\n" +
                "Force Off - Forces all states to WD off\n" +
                "Disabled - Don't try to fix anything and don't warn even if it looks broken"));
            
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null) {
                container.Add(VRCFuryEditorUtils.DebugBox("No avatar descriptor"));
            } else {
                var analysis = FixWriteDefaultsService.DetectExistingWriteDefaults(avatar);
                var output = new List<string> {
                    $"Auto mode = {(analysis.shouldBeOnIfWeAreInControl ? "WD on" : "WD off")}",
                    $"Disabled compliance mode = {(analysis.shouldBeOnIfWeAreNotInControl ? "WD on" : "WD off")}",
                    "",
                    $"Debug info: {analysis.debugInfo}"
                };
                if (analysis.isBroken) {
                    output.Add("");
                    output.Add("Avatar base has broken mixed write defaults!");
                    output.Add("Here are the states that don't match:");
                    output.Add(analysis.weirdStates.JoinWithMore(20));
                }
                container.Add(VRCFuryEditorUtils.DebugBox(output.Join('\n')));
            }
            
            return container;
        }
    }
}
