using System;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks.UdonCleaner {
    /**
     * We prevent UdonSharp from updating the syncMethod on its backing behaviours (to reduce prefab overrides),
     * under the assumption that it will be fixed during play mode / the upload.
     * However, the VRCSDK gets upset if Object Sync shares with a manual syncmode component. We need to defer that check
     * until after the build, so we just remove it from the gui.
     */
    internal static class AllowObjectSyncManualUdonHook {
        private const string ObjectSyncManualUdonError = "Object Sync cannot share an object with a manually synchronized Udon Behaviour";

        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(AllowObjectSyncManualUdonHook),
                nameof(Prefix),
                "VRCSdkControlPanel",
                "OnGUIError"
            );
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix(string __1) {
            if (!SimplifyUdonSerializationMenuItem.Get()) return true;
            return __1 == null || !__1.Contains(ObjectSyncManualUdonError, StringComparison.Ordinal);
        }
    }
}
