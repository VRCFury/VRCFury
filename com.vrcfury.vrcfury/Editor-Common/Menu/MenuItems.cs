using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VF.Menu {
    internal static class MenuItems {
        private const string prefix = "Tools/VRCFury/";
        
        public const string update = prefix + "Update VRCFury";
        public const int updatePriority = 1000;

        public const string testCopy = prefix + "Build an Editor Test Copy";
        public const int testCopyPriority = 1221;

        public const string sps = prefix + "SPS/";
        public const int spsPriority = 1310;
        public const string createSocket = sps + "Create Socket";
        public const int createSocketPriority = spsPriority;
        public const string createPlug = sps + "Create Plug";
        public const int createPlugPriority = spsPriority + 1;
        public const string upgradeLegacyHaptics = sps + "Upgrade DPS to SPS";
        public const int upgradeLegacyHapticsPriority = spsPriority + 2;

        public const string utilities = prefix + "Utilities/";
        public const int utilitiesPriority = 1311;
        public const string nukeZawoo = utilities + "Nuke Zawoo";
        public const int nukeZawooPriority = utilitiesPriority;
        public const string detectDuplicatePhysbones = utilities + "Detect Duplicate Physbones";
        public const int detectDuplicatePhysbonesPriority = utilitiesPriority + 3;
        public const string reserialize = utilities + "Reserialize All VRCFury Assets";
        public const int reserializePriority = utilitiesPriority + 4;
        public const string debugCopy = utilities + "Make debug copy during build";
        public const int debugCopyPriority = utilitiesPriority + 6;
        public const string recompileAll = utilities + "Recompile all scripts";
        public const int recompileAllPriority = utilitiesPriority + 7;
        public const string blockScriptImports = utilities + "Block Script Imports";
        public const int blockScriptImportsPriority = utilitiesPriority + 8;
        public const string disableDbtMerging = utilities + "Disable DBT Merging";
        public const int disableDbtMergingPriority = utilitiesPriority + 9;
        public const string spsDevMode = utilities + "Enable SPS Internal Dev Mode";
        public const int spsDevModePriority = utilitiesPriority + 10;
        public const string udonCleanerUninstallAgain = utilities + "Re-run Udon Cleaner Uninstall";
        public const int udonCleanerUninstallAgainPriority = utilitiesPriority + 11;
        public const string applySuperSampledUiMaterialOverrides = utilities + "Apply VRCSuperSampledUIMaterial Overrides";
        public const int applySuperSampledUiMaterialOverridesPriority = utilitiesPriority + 13;
        public const string sceneDirtyLogger = utilities + "Log why Scene is dirtied";
        public const int sceneDirtyLoggerPriority = utilitiesPriority + 14;
        public const string logVfInitTimings = utilities + "Log Timings";
        public const int logVfInitTimingsPriority = utilitiesPriority + 16;
        public const string logAvatarPreprocessors = utilities + "Log Avatar Preprocessors";
        public const int logAvatarPreprocessorsPriority = utilitiesPriority + 17;

        public const string settings = prefix + "Settings/";
        public const int settingsPriority = 1312;
        public const string playMode = settings + "Enable VRCFury in play mode";
        public const int playModePriority = settingsPriority;
        public const string uploadMode = settings + "Enable VRCFury for uploads";
        public const int uploadModePriority = settingsPriority + 1;
        public const string constrainedProportions = settings + "Automatically enable Constrained Proportions";

        public const int constrainedProportionsPriority = settingsPriority + 100;
        public const string hapticToggle = settings + "Enable SPS Haptics";
        public const int hapticTogglePriority = settingsPriority + 101;
        public const string dpsAutoUpgrade = settings + "Automatically upgrade DPS and TPS to SPS";
        public const int dpsAutoUpgradePriority = settingsPriority + 102;
        public const string boundingBoxFix = settings + "Automatically fix bounding boxes";
        public const int boundingBoxFixPriority = settingsPriority + 103;
        public const string autoUpgradeConstraints = settings + "Automatically upgrade to VRC Constraints";
        public const int autoUpgradeConstraintsPriority = settingsPriority + 104;
        public const string unpackWarning = settings + "Warn when unpacking prefabs";
        public const int unpackWarningPriority = settingsPriority + 105;
        public const string alignMobile = settings + "Align Mobile Parameters to match Desktop";
        public const int alignMobilePriority = settingsPriority + 106;
        public const string udonCleaner = settings + "Enable Udon Cleaner (Alpha)";
        public const int udonCleanerPriority = settingsPriority + 108;
        
        public const string compressHeader = settings + "When avatar is over parameter limit:";
        public const int compressHeaderPriority = settingsPriority + 200;
        public const string compressCompress = settings + "Compress parameters to fit";
        public const int compressCompressPriority = settingsPriority + 201;
        public const string compressAsk = settings + "Ask";
        public const int compressAskPriority = settingsPriority + 202;
        public const string compressFail = settings + "Fail the build (Vanilla Behaviour)";
        public const int compressFailPriority = settingsPriority + 203;
        
#if UNITY_2021_1_OR_NEWER
        [MenuItem(recompileAll, priority = recompileAllPriority)]
        private static void RecompileAll() {
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
#endif

        public static void AddHierarchyCommand(string label, Func<GameObject, bool> show, Action<GameObject> run) {
            SceneHierarchyHooks.addItemsToGameObjectContextMenu += (menu, obj) => {
                if (obj == null || !show(obj)) return;
                menu.AddItem(new GUIContent("VRCFury/" + label), false, () => {
                    if (obj != null) run(obj);
                });
            };
        }

    }
}
