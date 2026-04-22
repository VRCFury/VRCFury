using UnityEditor;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class DisableVpmResolverAutoInitHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchResolveIsNeeded = HarmonyUtils.Patch(
                typeof(DisableVpmResolverAutoInitHook),
                nameof(ResolveIsNeededPrefix),
                "VRC.PackageManagement.Core.Types.Packages.VPMProjectManifest",
                "ResolveIsNeeded"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchResolveIsNeeded.apply();
        }

        private static bool ResolveIsNeededPrefix(ref bool __result) {
            if (!DisableVpmResolverInitMenuItem.Get()) return true;
            __result = false;
            return false;
        }
    }
}

