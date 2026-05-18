using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using VF.Menu;
using VF.Utils;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace VF.Hooks.UdonCleaner {
    /**
     * Better handling for syncMethod of udonsharp behaviours.
     * * Allows None to coexist with synced types.
     * * Sets the physical field non-destructively instead of changing the scene all the time
     */
    internal static class UdonCleanerSyncMethodManager {
        private abstract class Reflection : ReflectionHelper {
            private const string MixedSyncMethodsWarning = "You are mixing sync methods between UdonBehaviours on the same game object, this will cause all behaviours to use the sync method of the last component on the game object.";
            public static readonly HarmonyUtils.PatchObj PatchSyncMethodGet = HarmonyUtils.Patch(
                typeof(UdonBehaviour).GetProperty(nameof(UdonBehaviour.SyncMethod))?.GetGetMethod(),
                (typeof(UdonCleanerSyncMethodManager), nameof(OnSyncMethodGet))
            );
            public static readonly HarmonyUtils.PatchObj PatchSyncMethodSet = HarmonyUtils.Patch(
                typeof(UdonBehaviour).GetProperty(nameof(UdonBehaviour.SyncMethod))?.GetGetMethod(),
                (typeof(UdonCleanerSyncMethodManager), nameof(OnSyncMethodSet))
            );
            public static readonly HarmonyUtils.PatchObj PatchUpdateSyncModes = HarmonyUtils.Patch(
                ("UdonSharpEditor.UdonSharpEditorManager", "UpdateSyncModes"),
                (typeof(UdonCleanerSyncMethodManager), nameof(OnUpdateSyncModes))
            );
            public static readonly HarmonyUtils.PatchObj PatchHelpBox = HarmonyUtils.Patch(
                typeof(EditorGUILayout).GetMethod(nameof(EditorGUILayout.HelpBox), new[] { typeof(string), typeof(MessageType) }),
                (typeof(UdonCleanerSyncMethodManager), nameof(OnHelpBox))
            );
            public static readonly FieldInfo _syncMethod = typeof(UdonBehaviour).VFField("_syncMethod");
        }

        [VFInit]
        private static void Init() {
            if (!IsActive()) return;
            Reflection.PatchSyncMethodGet.apply();
            Reflection.PatchSyncMethodSet.apply();
            Reflection.PatchUpdateSyncModes.apply();
            Reflection.PatchHelpBox.apply();
        }

        public static bool IsActive() {
            return UdonCleanerMenuItem.Get() && ReflectionHelper.IsReady<Reflection>();
        }

        private static bool OnUpdateSyncModes() {
            return false;
        }

        private static bool OnHelpBox(string __0) {
            // U# incorrectly shows this warning when you mix None and non-None sync types.
            if (IsActive() && __0 != null && __0.Contains(Reflection.MixedSyncMethodsWarning)) {
                return false;
            }
            return true;
        }

        private static bool OnSyncMethodGet(UdonBehaviour __instance, ref Networking.SyncType __result) {
            var myChoice = GetPreferredSyncMethod(__instance);
            if (myChoice == Networking.SyncType.None || myChoice == Networking.SyncType.Continuous) {
                __result = myChoice;
                return false;
            }

            if (__instance.GetComponent<VRCObjectSync>() != null) {
                __result = Networking.SyncType.Continuous;
                return false;
            }

            var others = __instance.GetComponents<UdonBehaviour>().Where(o => o != __instance).ToArray();
            if (others.Any(o => GetPreferredSyncMethod(o) == Networking.SyncType.Continuous)) {
                __result = Networking.SyncType.Continuous;
                return false;
            }

            __result = Networking.SyncType.Manual;
            return false;
        }

        private static bool OnSyncMethodSet(UdonBehaviour __instance) {
            if (UdonCleanerOnSaveHooks.GetForcedSyncMethodFromUSharp(__instance) != BehaviourSyncMode.Any) {
                return false;
            }
            return true;
        }

        private static Networking.SyncType GetPreferredSyncMethod(UdonBehaviour ub) {
            var preferred = (Networking.SyncType)Reflection._syncMethod.GetValue(ub);
            var forced = UdonCleanerOnSaveHooks.GetForcedSyncMethodFromUSharp(ub);
            if (forced == BehaviourSyncMode.Continuous) preferred = Networking.SyncType.Continuous;
            if (forced == BehaviourSyncMode.Manual) preferred = Networking.SyncType.Manual;
            if (forced == BehaviourSyncMode.NoVariableSync) preferred = Networking.SyncType.Manual;
            if (forced == BehaviourSyncMode.None) preferred = Networking.SyncType.None;
            return preferred;
        }
    }
}

