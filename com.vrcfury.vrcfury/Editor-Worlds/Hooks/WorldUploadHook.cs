using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Features;
using VF.Menu;

namespace VF.Hooks {
    internal class WorldUploadHook : IProcessSceneWithReport {
        public int callbackOrder => -10000;

        public void OnProcessScene(Scene scene, BuildReport report) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return;
            if (IsActuallyUploadingWorldHook.Get() && !UseInUploadMenuItem.Get()) return;

            TmpFilePackage.Cleanup();
            BuildInjectUnityActions.Process(scene);
            BuildSps.Process(scene);
            BuildMarker.Process(scene);
            ComponentInjects.Wire(scene);
        }
    }
}
