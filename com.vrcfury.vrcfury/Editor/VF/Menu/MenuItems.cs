using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Model;
using VF.Utils;

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
        public const string upgradeLegacyHaptics = sps + "Upgrade legacy haptics";
        public const int upgradeLegacyHapticsPriority = spsPriority + 2;

        public const string utilities = prefix + "Utilities/";
        public const int utilitiesPriority = 1311;
        public const string nukeZawoo = utilities + "Utilities/Nuke Zawoo";
        public const int nukeZawooPriority = utilitiesPriority;
        public const string unusedBones = utilities + "Nuke unused bones";
        public const int unusedBonesPriority = utilitiesPriority + 1;
        public const string listComponents = utilities + "List All Components";
        public const int listComponentsPriority = utilitiesPriority + 2;
        public const string detectDuplicatePhysbones = utilities + "Detect Duplicate Physbones";
        public const int detectDuplicatePhysbonesPriority = utilitiesPriority + 3;
        public const string reserialize = utilities + "Reserialize All VRCFury Assets";
        public const int reserializePriority = utilitiesPriority + 4;
        public const string uselessOverrides = utilities + "Cleanup Useless Overrides";
        public const int uselessOverridesPriority = utilitiesPriority + 5;
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
        public const string dpsAutoUpgrade = settings + "Auto-Upgrade DPS with contacts";
        public const int dpsAutoUpgradePriority = settingsPriority + 102;
        public const string boundingBoxFix = settings + "Automatically fix bounding boxes";
        public const int boundingBoxFixPriority = settingsPriority + 103;
        public const string autoUpgradeConstraints = settings + "Automatically upgrade to VRC Constraints";
        public const int autoUpgradeConstraintsPriority = settingsPriority + 104;
        public const string unpackWarning = settings + "Warn when unpacking prefabs";
        public const int unpackWarningPriority = settingsPriority + 105;
        public const string alignMobile = settings + "Align Mobile Parameters to match Desktop";
        public const int alignMobilePriority = settingsPriority + 106;
        
        public const string compressHeader = settings + "When avatar is over parameter limit:";
        public const int compressHeaderPriority = settingsPriority + 200;
        public const string compressCompress = settings + "Compress parameters to fit";
        public const int compressCompressPriority = settingsPriority + 201;
        public const string compressAsk = settings + "Ask";
        public const int compressAskPriority = settingsPriority + 202;
        public const string compressFail = settings + "Fail the build (Vanilla Behaviour)";
        public const int compressFailPriority = settingsPriority + 203;

        [MenuItem(upgradeLegacyHaptics, priority = upgradeLegacyHapticsPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                SpsUpgrader.Run();
            });
        }

        [MenuItem(upgradeLegacyHaptics, true)]
        private static bool Check() {
            return SpsUpgrader.Check();
        }
        
        [MenuItem("GameObject/VRCFury/Create SPS Socket", priority = 40)]
        [MenuItem(createSocket, priority = createSocketPriority)]
        public static void RunSocket() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                HapticsMenuItem.Create(false);
            });
        }
        
        [MenuItem("GameObject/VRCFury/Create SPS Plug", priority = 41)]
        [MenuItem(createPlug, priority = createPlugPriority)]
        public static void RunPlug() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                HapticsMenuItem.Create(true);
            });
        }

        /*
        [MenuItem(bakeHaptic, priority = bakeHapticPriority)]
        public static void RunBake() {
            HapticsMenuItem.RunBake();
        }
        */

        [MenuItem(nukeZawoo, priority = nukeZawooPriority)]
        private static void NukeZawooParts() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                ZawooDeleter.Run(MenuUtils.GetSelectedAvatar());
            });
        }
        
        [MenuItem(nukeZawoo, true)]
        private static bool CheckNukeZawooParts() {
            return MenuUtils.GetSelectedAvatar() != null;
        }

        [MenuItem("GameObject/VRCFury/Build an Editor Test Copy", priority = 42)]
        [MenuItem(testCopy, priority = testCopyPriority)]
        private static void RunForceRun() {
            VRCFuryTestCopyMenuItem.RunBuildTestCopy();
        }
        [MenuItem("GameObject/VRCFury/Build an Editor Test Copy", true)]
        [MenuItem(testCopy, true)]
        private static bool CheckForceRun() {
            return VRCFuryTestCopyMenuItem.CheckBuildTestCopy();
        }
        
#if UNITY_2022_1_OR_NEWER
        [MenuItem(recompileAll, priority = recompileAllPriority)]
        private static void RecompileAll() {
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
#endif

        [MenuItem(listComponents, priority = listComponentsPriority)]
        private static void ListChildComponents() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                var obj = Selection.activeGameObject.asVf();
                if (obj == null) return;
                var list = new List<string>();
                foreach (var c in obj.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    if (c == null || c is Transform) continue;
                    var type = c.GetType().Name;
                    if (c is VRCFury vf) {
                        type += " (" + vf.GetAllFeatures().Select(f => f.GetType().Name).Join(',') + ")";
                    }
                    list.Add(type  + " in " + c.owner().GetPath(obj));
                }

                var output = $"List of components on {obj}:\n" + list.Join('\n');
                GUIUtility.systemCopyBuffer = output;

                DialogUtils.DisplayDialog(
                    "Debug",
                    $"Found {list.Count} components in {obj.name} and copied them to clipboard",
                    "Ok"
                );
            });
        }

        [MenuItem(reserialize, priority = reserializePriority)]
        private static void Reserialize() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                var doIt = DialogUtils.DisplayDialog(
                    "VRCFury",
                    "This is intended for VRCFury developers only, in order to quickly" +
                    " refresh serialization of all VRCF components in a project." +
                    "\n\n" +
                    "THIS WILL LOAD ALL SCENES AND TAKE A LONG TIME!" +
                    "\n\n" +
                    "Continue?",
                    "Ok",
                    "Cancel"
                );
                if (!doIt) return;

                BulkUpgradeUtils.WithAllScenesOpen(() => {
                    foreach (var vrcf in BulkUpgradeUtils.FindAll<VRCFuryComponent>()) {
                        vrcf.Upgrade();
                    }
                });

                DialogUtils.DisplayDialog(
                    "VRCFury",
                    $"Done",
                    "Ok"
                );
            });
        }
    }
}
