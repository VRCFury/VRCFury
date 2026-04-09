using UnityEditor;
using VF.Builder.Haptics;
using VF.Utils;

namespace VF.Hooks {
    internal static class VRCFuryWorldHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            VFGameObject.getUploadRoots = obj => VFGameObject.GetRoots(obj.scene);
            SpsConfigurer.getIsActuallyUploading = IsActuallyUploadingWorldHook.Get;
            PreventComponentDeletionHook.getIsActuallyUploading = IsActuallyUploadingWorldHook.Get;
        }
    }
}
