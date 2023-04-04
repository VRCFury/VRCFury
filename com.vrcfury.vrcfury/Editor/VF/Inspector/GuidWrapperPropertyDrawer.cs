using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomPropertyDrawer(typeof(GuidWrapper<>), true)]
    public class GuidWrapperPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var obj = prop.FindPropertyRelative("obj");
            var output = new VisualElement();
            output.AddToClassList("vfGuidWrapper");
            output.Add(VRCFuryEditorUtils.Prop(obj));
            var guid = prop.FindPropertyRelative("guid");
            var fileId = prop.FindPropertyRelative("fileID");
            output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (obj.objectReferenceValue == null && !string.IsNullOrEmpty(guid.stringValue))
                    return VRCFuryEditorUtils.WrappedLabel($"Missing asset: {guid.stringValue}:{fileId.longValue}");
                else
                    return new VisualElement();
            }, obj, guid, fileId));
            return output;
        }
    }
}
