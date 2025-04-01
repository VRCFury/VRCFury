using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Menu {
    internal static class VRCFuryTestCopyMenuItem {

        public static void RunBuildTestCopy() {
            var originalObject = MenuUtils.GetSelectedAvatar();
            BuildTestCopy(originalObject);
        }
        
        public static void BuildTestCopy(VFGameObject originalObject) {
            VRCFPrefabFixer.Fix(new[] {originalObject});

            var cloneName = "VRCF Test Copy for " + originalObject.name;
            var exists = VFGameObject.GetRoots(originalObject.scene)
                .FirstOrDefault(o => o.name == cloneName);
            if (exists != null) {
                exists.Destroy();
            }
            var clone = originalObject.Clone();
            clone.name = originalObject.name + "(Clone)";
            if (!VRCBuildPipelineCallbacks.OnPreprocessAvatar(clone)) {
                clone.Destroy();
                return;
            }

            clone.active = true;
            if (clone.scene != originalObject.scene) {
                SceneManager.MoveGameObjectToScene(clone, originalObject.scene);
            }
            clone.name = cloneName;
            Selection.SetActiveObjectWithContext(clone, clone);
        }

        public static bool CheckBuildTestCopy() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
    }
}
