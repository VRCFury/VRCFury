using UnityEditor;
using UnityEngine;

namespace VF.Component {
    internal abstract class VRCFuryPlayComponent : MonoBehaviour {
#if UNITY_EDITOR
        private void OnValidate() {
            hideFlags |= HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            // Ensure this deletes itself if it ever winds up outside play mode
            if (!Application.isPlaying) {
                EditorApplication.delayCall += () => DestroyImmediate(this);
            }
        }
#endif
    }
}
