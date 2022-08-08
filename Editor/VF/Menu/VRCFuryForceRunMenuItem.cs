using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;

namespace VF.Menu {
    public class VRCFuryForceRunMenuItem {

        public static void RunFakeUpload() {
            var obj = MenuUtils.GetSelectedAvatar();
            var builder = new VRCFuryBuilder();
            builder.TestRun(obj);
        }

        public static bool CheckFakeUpload() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
            return false;
        }
        
        public static void RunPurge() {
            var obj = MenuUtils.GetSelectedAvatar();
            VRCFuryBuilder.DetachFromAvatar(obj);
        }

        public static bool CheckPurge() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            return true;
        }
    }
}
