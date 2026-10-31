using com.vrcfury.udon.Components;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Component;
using VF.Utils;

namespace VF.Inspector {
    [CustomEditor(typeof(UdonDiInjectField), true)]
    internal class UdonDiInjectFieldEditor : VRCFuryComponentEditor<UdonDiInjectField> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, UdonDiInjectField target) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "The given target field on an udon behaviour on this object will be automatically set during the upload " +
                "to matching components in the scene."));
            c.Add(VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("targetField"),
                "Field on this object to inject into"
            ));
            var matchAllProp = serializedObject.FindProperty("matchAll");
            var sourceMode = new RadioButtonGroup("Component source") {
                choices = new List<string> {
                    "UdonDI Registered Components Only",
                    "All Components of Type in Scene"
                }
            };
            sourceMode.SetValueWithoutNotify(matchAllProp.boolValue ? 1 : 0);

            var registeredName = VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("registeredName"),
                "ID of registered component (may be empty)"
            );
            registeredName.style.display = matchAllProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;

            sourceMode.RegisterValueChangedCallback(evt => {
                matchAllProp.boolValue = evt.newValue == 1;
                serializedObject.ApplyModifiedProperties();
                registeredName.style.display = matchAllProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            });

            c.Add(sourceMode);
            c.Add(registeredName);
            return c;
        }
    }
}
