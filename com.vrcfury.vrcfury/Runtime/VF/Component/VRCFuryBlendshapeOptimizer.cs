using UnityEditor;
using UnityEngine;

namespace VF.Component {
    // TODO: Delete this class
    public class VRCFuryBlendshapeOptimizer : MonoBehaviour {
        void OnValidate() {
            EditorApplication.delayCall += () => {
                var c = gameObject.AddComponent<VRCFuryComponentNew>();
                c.json = "{\"type\":\"blendshapeOptimizer\"}";
                DestroyImmediate(this);
            };
        }
    }
}