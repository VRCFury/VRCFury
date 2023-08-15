using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VF.Inspector;

namespace VF.Component {
    [CustomPropertyDrawer(typeof(ObjectWrapper<>), true)]
    public class ObjectWrapperPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var obj = prop.FindPropertyRelative("obj");
            var backupGuid = prop.FindPropertyRelative("backupGuid");
            var backupFileId = prop.FindPropertyRelative("backupFileId");
            var backupName = prop.FindPropertyRelative("backupName");
            var backupFile = prop.FindPropertyRelative("backupFile");

            var output = new VisualElement();
            output.AddToClassList("vfGuidWrapper");

            var field = new ObjectField();
            output.Add(field);
            field.objectType = UnitySerializationUtils.GetPropertyType(obj);
            field.value = obj.objectReferenceValue;
            field.RegisterValueChangedCallback(cb => {
                if (cb.newValue == null) {
                    obj.objectReferenceValue = null;
                    backupGuid.stringValue = "";
                    backupFileId.longValue = 0;
                    backupName.stringValue = "";
                    backupFile.stringValue = "";
                } else {
                    obj.objectReferenceValue = cb.newValue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(cb.newValue, out var guid, out long fileId);
                    backupGuid.stringValue = guid;
                    backupFileId.longValue = fileId;
                    backupName.stringValue = cb.newValue.name;
                    backupFile.stringValue = AssetDatabase.GUIDToAssetPath(guid) ?? "";
                }
                prop.serializedObject.ApplyModifiedProperties();
            });

            output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (obj.objectReferenceValue != null || string.IsNullOrWhiteSpace(backupGuid.stringValue)) return new VisualElement();

                var missingId = "";
                if (!string.IsNullOrWhiteSpace(backupName.stringValue)) {
                    missingId += backupName.stringValue;
                    if (!string.IsNullOrWhiteSpace(backupFile.stringValue)) {
                        missingId += " from " + backupFile.stringValue;
                    }
                } else {
                    missingId += backupGuid.stringValue + ":" + backupFileId.longValue;
                }
                return VRCFuryEditorUtils.WrappedLabel($"Missing asset: {missingId}");
            }, obj, backupGuid, backupFileId, backupName, backupFile));
            return output;
        }
    }
}
