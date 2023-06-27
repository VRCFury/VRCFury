using UnityEditor;
using UnityEngine.UIElements;
using VF.Inspector;

namespace VF.Component {
    [CustomPropertyDrawer(typeof(ObjectWrapper<>), true)]
    public class ObjectWrapperPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var obj = prop.FindPropertyRelative("obj");
            var output = new VisualElement();
            output.AddToClassList("vfGuidWrapper");
            output.Add(VRCFuryEditorUtils.Prop(obj));
            var backupId = prop.FindPropertyRelative("backupId");
            var backupName = prop.FindPropertyRelative("backupName");
            var backupFile = prop.FindPropertyRelative("backupFile");
            output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (obj.objectReferenceValue != null || string.IsNullOrWhiteSpace(backupId.stringValue)) return new VisualElement();

                var missingId = "";
                if (!string.IsNullOrWhiteSpace(backupName.stringValue)) {
                    missingId += backupName.stringValue;
                    if (!string.IsNullOrWhiteSpace(backupFile.stringValue)) {
                        missingId += " from " + backupFile.stringValue;
                    }
                } else {
                    missingId += backupId.stringValue;
                }
                return VRCFuryEditorUtils.WrappedLabel($"Missing asset: {missingId}");
            }, obj, backupId, backupName, backupFile));
            return output;
        }
    }
}
