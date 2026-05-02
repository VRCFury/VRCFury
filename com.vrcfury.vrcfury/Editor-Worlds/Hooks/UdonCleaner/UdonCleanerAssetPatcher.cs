using System;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using VF.Menu;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;

namespace VF.Hooks.UdonCleaner {
    /**
     * Patches the VRCSDK to use assets from our asset manager instead of trying to maintain its own connections
     * through component fields.
     */
    internal static class UdonCleanerAssetPatcher {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchGetAllUdonSharpPrograms = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), nameof(UdonSharpProgramAsset.GetAllUdonSharpPrograms)),
                (typeof(UdonCleanerAssetPatcher), nameof(OnGetAllUdonSharpPrograms))
            );
            public static readonly HarmonyUtils.PatchObj PatchGetSerializedProgramAssetWithoutRefresh = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), "GetSerializedProgramAssetWithoutRefresh"),
                (typeof(UdonCleanerAssetPatcher), nameof(OnGetSerializedProgramAssetWithoutRefresh))
            );
        }

        private static bool OnGetAllUdonSharpPrograms(ref UdonSharpProgramAsset[] __result) {
            __result = UdonCleanerAssetManager.GetAllUSharpPrograms();
            return false;
        }

        private static bool OnGetSerializedProgramAssetWithoutRefresh(UdonSharpProgramAsset __instance, ref AbstractSerializedUdonProgramAsset __result) {
            __result = UdonCleanerAssetManager.GetSerializedForProgram(__instance);
            return false;
        }

        public static AbstractUdonProgramSource programSource_get(UdonBehaviour ub) {
            foreach (var usb in ub.GetComponents<UdonSharpBehaviour>()) {
                if (UdonSharpEditorUtility.GetBackingUdonBehaviour(usb) == ub) {
                    var script = MonoScript.FromMonoBehaviour(usb);
                    return UdonCleanerAssetManager.GetProgramForUSharpScript(script);
                }
            }
            return ub.programSource;
        }

        private static void programSource_set(UdonBehaviour ub, AbstractUdonProgramSource program) {
            // do nothing!
        }

        public static AbstractSerializedUdonProgramAsset serializedProgramAsset_get(UdonBehaviour ub) {
            var program = programSource_get(ub);
            if (program == null) return null;
            return UdonCleanerAssetManager.GetSerializedForProgram(program);
        }

        private static void serializedProgramAsset_set(UdonBehaviour ub, AbstractSerializedUdonProgramAsset program) {
            // do nothing!
        }

        private static AbstractSerializedUdonProgramAsset serializedUdonProgramAsset_get(UdonProgramAsset program) {
            return UdonCleanerAssetManager.GetSerializedForProgram(program);
        }

        private static void serializedUdonProgramAsset_set(UdonProgramAsset ub, AbstractSerializedUdonProgramAsset program) {
            // do nothing!
        }

        [VFInit]
        private static void Init() {
            if (!UdonCleanerMenuItem.Get()) return;
            if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
            if (!ReflectionHelper.IsReady<Reflection>()) return;

            Reflection.PatchGetAllUdonSharpPrograms.apply();
            Reflection.PatchGetSerializedProgramAssetWithoutRefresh.apply();

            HarmonyTranspiler.TranspileVarAccess(
                ReflectionUtils.GetUserAssemblies().Where(a => a != typeof(UdonCleanerAssetPatcher).Assembly),
                typeof(UdonCleanerAssetManager),
                (UdonCleanerReflection.programSource, nameof(programSource_get), nameof(programSource_set)),
                (UdonCleanerReflection.serializedProgramAsset, nameof(serializedProgramAsset_get), nameof(serializedProgramAsset_set)),
                (UdonCleanerReflection.serializedUdonProgramAsset, nameof(serializedUdonProgramAsset_get), nameof(serializedUdonProgramAsset_set))
            );
        }
    }
}
