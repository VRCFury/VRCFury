using UnityEditor;
using UnityEngine.UIElements;
using VF.Component;

[CustomEditor(typeof(VRCFuryComponent), true)]
internal class VrcfWorldEditor : Editor {
    public override VisualElement CreateInspectorGUI() {
        return new Label("VRCFury is not yet available for world projects.");
    }
}
