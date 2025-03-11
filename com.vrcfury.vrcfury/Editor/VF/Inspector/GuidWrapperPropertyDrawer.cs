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
        public static VrcfObjectId GetId(SerializedProperty prop) {
            return VrcfObjectId.FromId(GetIdProp(prop).stringValue);
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

            objField.RegisterValueChangedCallback(change => {
                if (objRefProp.GetNoneType() == SerializedPropertyExtensions.NoneType.Missing) {
                    // keep whatever's in there
                    //Debug.Log("Missing");
                } else {
                    UpdateFallbackId(prop);
                    prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
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
                return VRCFuryEditorUtils.WrappedLabel($"Last seen at {missingId}");
            }, GetIdProp(prop)));
            return output;
        }
    }
}
