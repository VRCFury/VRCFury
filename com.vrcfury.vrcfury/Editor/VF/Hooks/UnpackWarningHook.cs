using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks {
    internal static class UnpackWarningHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var SceneHierarchy = typeof(EditorApplication).Assembly.GetType("UnityEditor.SceneHierarchy");
            if (SceneHierarchy == null) return;
            var prefix = typeof(UnpackWarningHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            var original = SceneHierarchy.GetMethod("UnpackPrefab", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (original != null) HarmonyUtils.Patch(original, prefix);
            original = SceneHierarchy.GetMethod("UnpackPrefabCompletely", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (original != null) HarmonyUtils.Patch(original, prefix);
        } 

        private static bool Prefix() {
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
