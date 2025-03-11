using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;

namespace VF.Inspector {
    [CustomPropertyDrawer(typeof(GuidWrapper<>), true)]
    internal class GuidWrapperPropertyDrawer : PropertyDrawer {
        private static SerializedProperty GetIdProp(SerializedProperty wrapper) {
            return wrapper.FindPropertyRelative("id");
        }
        private static SerializedProperty GetObjRefProp(SerializedProperty wrapper) {
            return wrapper.FindPropertyRelative("objRef");
        }
        public static void SetValue(SerializedProperty prop, Object val) {
            GetObjRefProp(prop).objectReferenceValue = val;
            UpdateFallbackId(prop);
        }
        public static void UpdateFallbackId(SerializedProperty prop) {
            GetIdProp(prop).stringValue = VrcfObjectId.ObjectToId(GetObjRefProp(prop).objectReferenceValue);
        }
        public static Object GetValue(SerializedProperty prop) {
            return VrcfObjectId.IdToObject<Object>(GetIdProp(prop).stringValue);
        }
        public static VrcfObjectId GetId(SerializedProperty prop) {
            return VrcfObjectId.FromId(GetIdProp(prop).stringValue);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var output = new VisualElement();

            var objField = new ObjectField();
            var type = fieldInfo.FieldType.GetField("typeDetector").FieldType;
            objField.objectType = type;
            objField.bindingPath = GetObjRefProp(prop).propertyPath;
            output.Add(objField);

            objField.RegisterValueChangedCallback(change => {
                if (change.newValue == null && change.previousValue != null) {
                    UpdateFallbackId(prop);
                    prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                } else if (change.newValue != null && change.newValue != change.previousValue) {
                    UpdateFallbackId(prop);
                    prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("Changed");
            });
            objField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Delete) {
                    SetValue(prop, null);
                    prop.serializedObject.ApplyModifiedProperties();
                }
            });
 
            output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (objField.value != null) return new VisualElement();

                var parsed = GetId(prop);
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
            }, GetIdProp(prop)));
            return output;
        }
    }
}
