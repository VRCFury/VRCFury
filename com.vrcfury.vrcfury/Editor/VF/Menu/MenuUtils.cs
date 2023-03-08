using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VF.Menu {
    public class MenuUtils {
        public static GameObject GetSelectedAvatar() {
            var obj = Selection.activeGameObject;
            while (obj != null) {
                var avatar = obj.GetComponent<VRCAvatarDescriptor>();
                if (avatar != null) return obj;
                obj = obj.transform.parent?.gameObject;
            }
            return null;
        }
    }
}
