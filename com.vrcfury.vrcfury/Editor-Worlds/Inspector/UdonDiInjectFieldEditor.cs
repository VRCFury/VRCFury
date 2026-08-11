using com.vrcfury.udon.Components;
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
                "to a matching component in the scene. The matching component must be have its own UdonDI - Register Component component."));
            c.Add(VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("targetField"),
                "Field on this object to inject into"
            ));
            c.Add(VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("registeredName"),
                "ID of registered component (may be empty)"
            ));
            return c;
        }
    }
}
