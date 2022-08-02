using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;

namespace VF.Menu {
    public class VRCFuryForceRunMenuItem {
        [MenuItem("Tools/VRCFury/Debug/Force Run on Selection", priority = 100)]
        private static void Run() {
            var obj = MenuUtils.GetSelectedAvatar();
            var builder = new VRCFuryBuilder();
            builder.SafeRun(obj);
        }

        [MenuItem("Tools/VRCFury/Debug/Force Run on Selection", true)]
        private static bool Check() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
            return false;
        }
        
        [MenuItem("Tools/VRCFury/Debug/Force Run as if uploading", priority = 101)]
        private static void RunFakeUpload() {
            var obj = MenuUtils.GetSelectedAvatar();
            var clone = Object.Instantiate(obj);
            if (clone.scene != obj.scene) SceneManager.MoveGameObjectToScene(clone, obj.scene);
            var builder = new VRCFuryBuilder();
            builder.SafeRun(obj, clone);
        }

        [MenuItem("Tools/VRCFury/Debug/Force Run as if uploading", true)]
        private static bool CheckFakeUpload() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
            return false;
        }
        
        [MenuItem("Tools/VRCFury/Debug/Purge from Selection", priority = 102)]
        private static void RunPurge() {
            var obj = MenuUtils.GetSelectedAvatar();
            VRCFuryBuilder.DetachFromAvatar(obj);
        }

        [MenuItem("Tools/VRCFury/Debug/Purge from Selection", true)]
        private static bool CheckPurge() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            return true;
        }
    }
}
