using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {

    [CustomEditor(typeof(VRCFuryTest), true)]
    public class VRCFuryTestEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            return VRCFuryEditorUtils.Error(
                "This avatar is a VRCFury editor test copy. Do not upload test copies, they are intended for" +
                " temporary in-editor testing only. Any changes made to this copy will be lost.");
        }
    }
    
}
