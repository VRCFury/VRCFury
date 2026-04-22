using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Features;
using VF.Menu;

namespace VF.Hooks {
    internal class WorldUploadHook : VrcfWorldPreprocessor {
        protected override int order => -10000;

        protected override void Process(Scene scene) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return;
            if (IsActuallyUploadingWorldHook.Get() && !UseInUploadMenuItem.Get()) return;

            BuildInjectUnityActions.Process(scene);
            BuildSps.Process(scene);
            BuildMarker.Process(scene);
        }
    }
}
