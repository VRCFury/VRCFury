using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
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
            
            string cached = null;
            container.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                if (cached != null) {
                    return cached;
                }
                
                var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatar == null) return "No avatar descriptor";

                var avatarControllers = VRCAvatarUtils.GetAllControllers(avatar)
                    .Select(c => {
                        var ctrl = c.controller;
                        while (ctrl is AnimatorOverrideController ov) ctrl = ov.runtimeAnimatorController;
                        if (ctrl == null) return null;
                        return new VFControllerWithVrcType(ctrl as AnimatorController, c.type);
                    })
                    .NotNull()
                    .ToArray();
                var analysis = FixWriteDefaultsService.DetectExistingWriteDefaults(avatarControllers);

                var output = new List<string>();
                output.Add($"Auto mode = {(analysis.shouldBeOnIfWeAreInControl ? "WD on" : "WD off")}");
                output.Add($"Disabled compliance mode = {(analysis.shouldBeOnIfWeAreNotInControl ? "WD on" : "WD off")}");
                output.Add($"");
                output.Add($"Debug info: {analysis.debugInfo}");
                if (analysis.isBroken) {
                    output.Add("");
                    output.Add("Avatar base has broken mixed write defaults!");
                    output.Add("Here are the states that don't match:");
                    if (analysis.weirdStates.Count > 20) {
                        output.Add(analysis.weirdStates.Take(20).Join('\n'));
                        output.Add("... and " + (analysis.weirdStates.Count-20) + " others");
                    } else {
                        output.Add(analysis.weirdStates.Join('\n'));
                    }
                }
                cached = output.Join('\n');
                return cached;
            }));
            
            return container;
        }
    }
}
