using UnityEditor;
using UnityEngine;
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

        [InitializeOnLoadMethod]
        private static void Init() {
            PreprocessorsRan.onDestroy = c => {
                var wasPlaying = Application.isPlaying;
                var go = c.gameObject;

                EditorApplication.delayCall += () => {
                    if (go == null) return; // Whole object was deleted
                    if (wasPlaying != Application.isPlaying)
                        return; // play mode changed, it could have been deleted when leaving play mode

                    if (!Application.isPlaying) {
                        var ok = EditorUtility.DisplayDialog(
                            "Warning",
                            "This is an avatar test copy. Attepting to save or upload this copy can result in catastrophic future breakage," +
                            " including avatars running preprocessors multiple times, asset files being lost (since they are temporary), or other" +
                            " irreversible issues. Are you sure you want to continue?",
                            "Yes, remove this component",
                            "Cancel"
                        );
                        if (ok) {
                            return;
                        }
                    }

                    go.AddComponent<PreprocessorsRan>();
                };
            };
        }
    }
    
}
