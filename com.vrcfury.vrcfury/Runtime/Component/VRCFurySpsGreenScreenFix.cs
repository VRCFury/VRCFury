using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.VrcfEditorOnly;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFurySpsGreenScreenFix : VRCFuryPlayComponent, IVrcfEditorOnly {
        public static Action onCreated;

        private void Start() {
            if (!Application.isPlaying) return;

            try {
// World projects create their own active camera when clientsim starts
#if !VRCF_WORLDS
                EnsureCameraExists();
#endif
                onCreated?.Invoke();
            } finally {
                DestroyImmediate(this);
            }
        }

        private static void EnsureCameraExists() {
            var hasCamera = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded)
                .SelectMany(scene => scene.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<Camera>())
                .Any(camera => camera.isActiveAndEnabled);
            if (hasCamera) return;

            var cameraObj = new GameObject("_SpsCameraFix");
            SceneManager.MoveGameObjectToScene(cameraObj, SceneManager.GetActiveScene());
            cameraObj.AddComponent<Camera>();
        }

    }
}
