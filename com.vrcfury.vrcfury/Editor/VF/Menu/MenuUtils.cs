using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

namespace VF.Menu {
    internal static class MenuUtils {
        public static VFGameObject GetSelectedAvatar() {
            var obj = Selection.activeGameObject.asVf();
            while (obj != null) {
                var avatar = obj.GetComponent<VRCAvatarDescriptor>();
                if (avatar != null) return obj;
                obj = obj.parent;
            }
            return null;
        }
    }
}
