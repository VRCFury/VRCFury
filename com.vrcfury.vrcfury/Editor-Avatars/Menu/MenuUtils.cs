using JetBrains.Annotations;
using UnityEditor;
using System;
using VF.Hooks;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Menu {
    internal static class MenuUtils {
        public static void AddAvatarHierarchyCommand(string label, Action<VFGameObject> run) {
            MenuItems.AddHierarchyCommand(label, obj => obj.GetComponent<VRCAvatarDescriptor>() != null, obj => run(obj));
        }

        [CanBeNull]
        public static VFGameObject GetSelectedAvatar() {
            var obj = Selection.activeGameObject.asVf();
            if (obj == null) return null;
            return obj.GetAvatarRoot();
        }
    }
}
