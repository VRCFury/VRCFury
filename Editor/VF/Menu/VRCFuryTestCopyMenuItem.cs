using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;

namespace VF.Menu {
    public static class VRCFuryTestCopyMenuItem {

        public static void RunBuildTestCopy() {
            var originalObject = MenuUtils.GetSelectedAvatar();
            BuildTestCopy(originalObject);
        }
        
        public static void BuildTestCopy(GameObject originalObject) {
            if (IsTestCopy(originalObject)) {
                EditorUtility.DisplayDialog("VRCFury Error", "This object is already a VRCF test copy.", "Ok");
                return;
            }

            VRCFPrefabFixer.Fix(originalObject);

            var cloneName = "VRCF Test Copy for " + originalObject.name;
            var exists = originalObject.scene
                .GetRootGameObjects()
                .FirstOrDefault(o => o.name == cloneName);
            if (exists) {
                Object.DestroyImmediate(exists);
            }
            var clone = Object.Instantiate(originalObject);
            if (!clone.activeSelf) {
                clone.SetActive(true);
            }
            if (clone.scene != originalObject.scene) {
                SceneManager.MoveGameObjectToScene(clone, originalObject.scene);
            }
            clone.name = cloneName;

            var builder = new VRCFuryBuilder();
            var result = builder.SafeRun(clone, originalObject);
            if (result) {
                VRCFuryBuilder.StripAllVrcfComponents(clone);
                clone.AddComponent<VRCFuryTest>();
                Selection.SetActiveObjectWithContext(clone, clone);
            } else {
                Object.DestroyImmediate(clone);
            }
        }

        public static bool IsTestCopy(GameObject obj) {
            return obj.GetComponentInChildren<VRCFuryTest>(true) != null;
        }

        public static bool CheckBuildTestCopy() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
    }
}
