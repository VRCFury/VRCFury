using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Features {
    internal static class BuildMarker {
        public static void Process(Scene scene) {
            var obj = new GameObject("VRCFury ran!");
            SceneManager.MoveGameObjectToScene(obj, scene);
        }
    }
}
