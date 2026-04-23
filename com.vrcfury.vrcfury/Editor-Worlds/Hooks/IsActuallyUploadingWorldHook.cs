using UnityEngine;

namespace VF.Hooks {
    internal static class IsActuallyUploadingWorldHook {
        public static bool Get() {
            return !Application.isPlaying;
        }
    }
}
