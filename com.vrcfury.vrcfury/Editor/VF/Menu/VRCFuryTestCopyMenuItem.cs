using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Menu {
    internal static class VRCFuryTestCopyMenuItem {

        public static void RunBuildTestCopy() {
            var originalObject = MenuUtils.GetSelectedAvatar();
            BuildTestCopy(originalObject);
        }
        
        public static void BuildTestCopy(VFGameObject originalObject) {
            if (IsTestCopy(originalObject)) {
                EditorUtility.DisplayDialog("VRCFury Error", "This object is already a VRCF editor test copy.", "Ok");
                return;
            }

            VRCFPrefabFixer.Fix(new[] {originalObject});

            var cloneName = "VRCF Test Copy for " + originalObject.name;
            var exists = VFGameObject.GetRoots(originalObject.scene)
                .FirstOrDefault(o => o.name == cloneName);
            if (exists) {
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
            clone.AddComponent<VRCFuryTest>();
            Selection.SetActiveObjectWithContext(clone, clone);
        }

        public static bool IsTestCopy(VFGameObject obj) {
            return obj.GetComponentsInSelfAndChildren<VRCFuryTest>().Length > 0;
        }

        public static bool CheckBuildTestCopy() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
    }
}
