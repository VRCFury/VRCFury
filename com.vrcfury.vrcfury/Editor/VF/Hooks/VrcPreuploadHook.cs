using UnityEngine;
using VF.Builder;
using VF.Menu;

namespace VF.Hooks {
    internal class VrcPreuploadHook : VrcfAvatarPreprocessor {
        protected override int order => -10000;

        protected override void Process(VFGameObject obj) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return;
            if (IsActuallyUploadingHook.Get() && !UseInUploadMenuItem.Get()) return;
            VRCFuryBuilder.RunMain(obj);
        }
    }
}
