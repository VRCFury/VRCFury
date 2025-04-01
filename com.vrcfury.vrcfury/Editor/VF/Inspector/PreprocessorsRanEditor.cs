using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {

    [CustomEditor(typeof(PreprocessorsRan), true)]
    internal class PreprocessorsRanEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            return VRCFuryEditorUtils.Error(
                "This object is an avatar TEST COPY." +
                " Avatar preprocessors have already run on this object." +
                " Do not upload test copies, they are intended for temporary in-editor testing only." +
                " Attempting to upload this avatar may result in breakage, as all preprocessors will be run again." +
                " Any changes made to this copy will be lost.");
        }
    }
    
}
