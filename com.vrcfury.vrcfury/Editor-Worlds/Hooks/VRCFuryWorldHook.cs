using UnityEditor;
using VF.Utils;

namespace VF.Hooks {
    internal static class VRCFuryWorldHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            VFGameObject.getUploadRoots = obj => obj.scene.Roots();
        }
    }
}
