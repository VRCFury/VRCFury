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
            return MenuUtils.GetSelectedAvatar() != null;
        }
    }
}
