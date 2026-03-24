using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryDebugInfo), true)]
    internal class VRCFuryDebugInfoEditor : VRCFuryComponentEditor<VRCFuryDebugInfo> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryDebugInfo target) {
            var row = new VisualElement();
            if (!string.IsNullOrEmpty(target.title)) {
                row.Add(VRCFuryEditorUtils.WrappedLabel(target.title).Bold());
            }
            if (!string.IsNullOrEmpty(target.debugInfo)) {
                row.Add(VRCFuryEditorUtils.WrappedLabel(target.debugInfo));
            }

            if (target.warn) {
                return VRCFuryEditorUtils.Warn(row);
            }
            return row;
        }
    }
}
