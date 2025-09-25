using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;
using Object = UnityEngine.Object;

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

        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var objRefProp = GetObjRefProp(prop);
            var idProp = GetIdProp(prop);
            
            var output = new VisualElement();

            var objField = new ObjectField();
            var type = fieldInfo.FieldType.GetField("typeDetector").FieldType;
            objField.objectType = type;
            objField.bindingPath = objRefProp.propertyPath;
            output.Add(objField);

            var lastSeenLabel = VRCFuryEditorUtils.WrappedLabel("");
            output.Add(lastSeenLabel);

            void UpdateLastSeenLabel() {
                if (objRefProp.objectReferenceValue != null || idProp.stringValue == "") {
                    lastSeenLabel.text = "";
                    lastSeenLabel.SetVisible(false);
                } else {
                    var parsed = VrcfObjectId.FromId(idProp.stringValue);
                    var missingId = "";
                    if (!string.IsNullOrWhiteSpace(parsed.objectName)) {
                        missingId = $"{parsed.objectName} from {parsed.fileName}";
                    } else if (!string.IsNullOrWhiteSpace(parsed.fileName)) {
                        missingId = parsed.fileName;
                    } else if (!string.IsNullOrWhiteSpace(parsed.guid)) {
                        missingId = $"GUID {parsed.guid}";
                    } else {
                        missingId = "?";
                    }
                    lastSeenLabel.text = $"Last seen at {missingId}";
                    lastSeenLabel.SetVisible(true);
                }
            }

            objField.RegisterValueChangedCallback(change => {
                if (change.newValue != null || change.previousValue != null) {
                    UpdateFallbackId(prop);
                    prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                UpdateLastSeenLabel();
            });
            objField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Delete) {
                    SetValue(prop, null);
                    prop.serializedObject.ApplyModifiedProperties();
                    UpdateLastSeenLabel();
                }
            });

            return output;
        }
    }
}
