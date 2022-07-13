using UnityEditor;
using VF.Builder;
using VF.Model;

namespace VF.Menu {
    public class VRCFuryForceRunMenuItem {
        [MenuItem("Tools/VRCFury/Force Run VRCFury on Selection")]
        private static void Run() {
            var obj = MenuUtils.GetSelectedAvatar();
            var builder = new VRCFuryBuilder();
            builder.SafeRun(obj);
        }

        [MenuItem("Tools/VRCFury/Force Run VRCFury on Selection", true)]
        private static bool Check() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
            return false;
        }
    }
}
