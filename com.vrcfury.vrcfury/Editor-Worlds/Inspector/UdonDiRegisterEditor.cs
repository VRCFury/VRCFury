using com.vrcfury.udon.Components;
using UnityEditor;
using UnityEngine.UIElements;

namespace VF.Inspector {
    [CustomEditor(typeof(UdonDiRegister), true)]
    internal class UdonDiRegisterEditor : VRCFuryComponentEditor<UdonDiRegister> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, UdonDiRegister target) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "The udon component on this object will be stored, and can be used in UdonDI - Inject Field components on other objects."));
            c.Add(VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("registeredName"),
                "Registered ID (Leave empty you only plan on having one!)"
            ));
            return c;
        }
    }
}
