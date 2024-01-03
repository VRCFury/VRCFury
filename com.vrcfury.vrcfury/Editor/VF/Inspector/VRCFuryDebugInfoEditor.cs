using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryDebugInfo), true)]
    public class VRCFuryDebugInfoEditor : VRCFuryComponentEditor<VRCFuryDebugInfo> {
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryDebugInfo target) {
            return VRCFuryEditorUtils.WrappedLabel(target.debugInfo);
        }
    }
}
