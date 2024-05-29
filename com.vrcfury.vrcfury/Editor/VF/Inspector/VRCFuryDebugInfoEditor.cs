using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryDebugInfo), true)]
    internal class VRCFuryDebugInfoEditor : VRCFuryComponentEditor<VRCFuryDebugInfo> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryDebugInfo target) {
            return VRCFuryEditorUtils.WrappedLabel(target.debugInfo);
        }
    }
}
