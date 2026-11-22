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
            GetIdProp(prop).stringValue = VrcfObjectId.ObjectToId(val);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var objRefProp = GetObjRefProp(prop);
            var idProp = GetIdProp(prop);
            
            var output = new VisualElement();

            var objField = new ObjectField();
            var type = fieldInfo.FieldType.VFField("typeDetector").FieldType;
            objField.objectType = type;
            output.Add(objField);

            var lastSeenLabel = VRCFuryEditorUtils.WrappedLabel("");
            output.Add(lastSeenLabel);

            void Refresh() {
                var reference = objRefProp.objectReferenceValue;
                objField.SetValueWithoutNotify(reference);
                if (reference != null || string.IsNullOrEmpty(idProp.stringValue)) {
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

            void Save(Object value) {
                SetValue(prop, value);
                prop.serializedObject.ApplyModifiedProperties();
                Refresh();
            }

            objField.RegisterValueChangedCallback(change => Save(change.newValue));
            objField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Delete) Save(null);
            });
            output.Add(VRCFuryEditorUtils.OnChange(objRefProp, Refresh));
            output.Add(VRCFuryEditorUtils.OnChange(idProp, Refresh));
            output.RegisterCallback<AttachToPanelEvent>(_ => Refresh());

            Refresh();
            return output;
        }
    }
}
