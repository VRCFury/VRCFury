using System;
using UnityEditor;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Model {
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    internal class PreprocessorsRan : MonoBehaviour, IVrcfEditorOnly {
        public enum State {
            AddedByHarmonyPatch,
            FirstPass,
            Finished
        }
        [NonSerialized] public State state = State.Finished;

        private void OnDestroy() {
            var wasPlaying = Application.isPlaying;
            var go = gameObject;

            EditorApplication.delayCall += () => {
                if (go == null) return; // Whole object was deleted
                if (wasPlaying != Application.isPlaying) return; // play mode changed, it could have been deleted when leaving play mode

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
        }
    }
}
