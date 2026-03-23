using UnityEditor;
using UnityEngine;

namespace VF.Component {
    internal static class VRCFuryPlayComponentEditor {
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCFuryPlayComponent.onValidate = c => {
                c.hideFlags |= HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable;
                // Ensure this deletes itself if it ever winds up outside play mode
                if (!Application.isPlaying) {
                    EditorApplication.delayCall += () => Object.DestroyImmediate(c);
                }
            };
        }
    }
}
