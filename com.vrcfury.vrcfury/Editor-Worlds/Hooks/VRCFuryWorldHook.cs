using UnityEditor;
using VF.Utils;

namespace VF.Hooks {
    internal static class VRCFuryWorldHook {
        [VFInit]
        private static void Init() {
            VFGameObject.getUploadRoots = obj => obj.scene.Roots();
        }
    }
}
