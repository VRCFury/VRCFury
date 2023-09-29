using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;

namespace VF.Inspector {
    [CustomPropertyDrawer(typeof(GuidWrapper<>), true)]
    public class GuidWrapperPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var id = prop.FindPropertyRelative("id");
            var objRef = prop.FindPropertyRelative("objRef");
            var output = new VisualElement();

            var objField = new ObjectField();
            var type = fieldInfo.FieldType.GetField("objOverride").FieldType;
            objField.objectType = type;
            objField.SetValueWithoutNotify(VrcfObjectId.IdToObject<Object>(id.stringValue));
            output.Add(objField);

            objField.RegisterValueChangedCallback(change => {
                id.stringValue = VrcfObjectId.ObjectToId(change.newValue);
                objRef.objectReferenceValue = change.newValue;
                id.serializedObject.ApplyModifiedProperties();
            });

            output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (objField.value != null) return new VisualElement();

                var parsed = VrcfObjectId.FromId(id.stringValue);
                var missingId = "";
                if (!string.IsNullOrWhiteSpace(parsed.objectName)) {
                    missingId = $"{parsed.objectName} from {parsed.fileName}";
                } else if (!string.IsNullOrWhiteSpace(parsed.fileName)) {
                    missingId = parsed.fileName;
                } else if (!string.IsNullOrWhiteSpace(parsed.guid)) {
                    missingId = $"GUID {parsed.guid}";
                } else {
                    return new VisualElement();
                }
                return VRCFuryEditorUtils.WrappedLabel($"Missing asset: {missingId}");
            }, id));
            return output;
        }
    }
}
