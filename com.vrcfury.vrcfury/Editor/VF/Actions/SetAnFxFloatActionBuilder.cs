using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Set an FX Float")]
    internal class SetAnFxFloatActionBuilder : ActionBuilder<FxFloatAction> {
        public AnimationClip Build(FxFloatAction model, AnimationClip offClip) {
            var onClip = NewClip();
            if (string.IsNullOrWhiteSpace(model.name)) {
                return onClip;
            }

            if (FullControllerBuilder.VRChatGlobalParams.Contains(model.name)) {
                throw new Exception("Set an FX Float cannot set built-in vrchat parameters");
            }

            onClip.SetAap(model.name, model.value);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var col = new VisualElement();

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name")).FlexGrow(1));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
            col.Add(row);
                
            col.Add(VRCFuryEditorUtils.Warn(
                "Warning: This will cause the FX parameter to be 'animated', which means it cannot be used" +
                " in a menu or otherwise controlled by VRChat."));

            return col;
        }
    }
}
