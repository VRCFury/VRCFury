using System.Reflection;
using UnityEditor;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    internal static class UnpackWarningHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(UnpackWarningHook),
                nameof(Prefix),
                "UnityEditor.SceneHierarchy",
                "UnpackPrefab"
            );
            HarmonyUtils.Patch(
                typeof(UnpackWarningHook),
                nameof(Prefix),
                "UnityEditor.SceneHierarchy",
                "UnpackPrefabCompletely"
            );
        } 

        private static bool Prefix() {
            if (!UnpackWarningMenuItem.Get()) return true;
            if (!EditorUtility.DisplayDialog(
                    "VRCFury says: Don't unpack!",
                    "Unpacking prefabs is an outdated recommendation!!\n\n" +
                    "* Unpacking will prevent you from easily getting updates to this asset\n\n" +
                    "* Unpacking will prevent you from receiving changes made to FBX models, and can cause your rig definition to become invalid\n\n" +
                    "* If you need to link clothes, reparent bones, or move child objects, use a non-destructive reparenting tool such as VRCFury Armature Link instead\n\n" +
                    "* Unpacking is basically never required for VRChat avatars or avatar assets!", 
                    "I don't care, Unpack Anyways (Not recommended)",
                    "Cancel"
            )) {
                return false;
            }
            return true;
        }
    }
}
