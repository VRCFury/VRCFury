using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VF.Menu {
    public class MenuUtils {
        public static GameObject GetSelectedAvatar() {
            if (Selection.activeTransform == null) return null;
            var obj = Selection.activeTransform.root.gameObject;
            var avatar = obj.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null) return null;
            return obj;
        }
    }
}
