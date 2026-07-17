using JetBrains.Annotations;
using UnityEditor;
using VF.Hooks;
using VF.Utils;

namespace VF.Menu {
    internal static class MenuUtils {
        [CanBeNull]
        public static VFGameObject GetSelectedAvatar() {
            var obj = Selection.activeGameObject.asVf();
            if (obj == null) return null;
            return obj.GetAvatarRoot();
        }
    }
}
