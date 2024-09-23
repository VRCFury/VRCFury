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

        public const string createSocket = prefix + "SPS/Create Socket";
        public const int createSocketPriority = 1301;
        public const string createPlug = prefix + "SPS/Create Plug";
        public const int createPlugPriority = 1302;
        public const string upgradeLegacyHaptics = prefix + "SPS/Upgrade legacy haptics";
        public const int upgradeLegacyHapticsPriority = 1303;

        public const string nukeZawoo = prefix + "Utilites/Nuke Zawoo";
        public const int nukeZawooPriority = 1311;
        public const string unusedBones = prefix + "Utilites/Nuke unused bones";
        public const int unusedBonesPriority = 1312;
        public const string listComponents = prefix + "Utilites/List All Components";
        public const int listComponentsPriority = 1313;
        public const string detectDuplicatePhysbones = prefix + "Utilites/Detect Duplicate Physbones";
        public const int detectDuplicatePhysbonesPriority = 1314;
        public const string reserialize = prefix + "Utilites/Reserialize All VRCFury Assets";
        public const int reserializePriority = 1315;
        public const string uselessOverrides = prefix + "Utilites/Cleanup Useless Overrides";
        public const int uselessOverridesPriority = 1316;
        public const string debugCopy = prefix + "Utilites/Make debug copy during build";
        public const int debugCopyPriority = 1317;
        public const string recompileAll = prefix + "Utilites/Recompile all scripts";
        public const int recompileAllPriority = 1318;
        
        public const string playMode = prefix + "Settings/Enable VRCFury in play mode";
        public const int playModePriority = 1321;
        public const string uploadMode = prefix + "Settings/Enable VRCFury for uploads";
        public const int uploadModePriority = 1322;
        public const string constrainedProportions = prefix + "Settings/Automatically enable Constrained Proportions";
        public const int constrainedProportionsPriority = 1323;
        public const string hapticToggle = prefix + "Settings/Enable SPS Haptics";
        public const int hapticTogglePriority = 1324;
        public const string dpsAutoUpgrade = prefix + "Settings/Auto-Upgrade DPS with contacts";
        public const int dpsAutoUpgradePriority = 1325;
        public const string boundingBoxFix = prefix + "Settings/Automatically fix bounding boxes";
        public const int boundingBoxFixPriority = 1326;
        public const string autoUpgradeConstraints = prefix + "Settings/Automatically upgrade to VRC Constraints";
        public const int autoUpgradeConstraintsPriority = 1327;

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

        [MenuItem(testCopy, priority = testCopyPriority)]
        private static void RunForceRun() {
            VRCFuryTestCopyMenuItem.RunBuildTestCopy();
        }
        [MenuItem(testCopy, true)]
        private static bool CheckForceRun() {
            return VRCFuryTestCopyMenuItem.CheckBuildTestCopy();
        }
        
        [MenuItem(recompileAll, priority = recompileAllPriority)]
        private static void RecompileAll() {
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

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
                        type += " (" + string.Join(",", vf.GetAllFeatures().Select(f => f.GetType().Name)) + ")";
                    }
                    list.Add(type  + " in " + c.owner().GetPath(obj));
                }

                var output = $"List of components on {obj}:\n" + string.Join("\n", list);
                GUIUtility.systemCopyBuffer = output;

                EditorUtility.DisplayDialog(
                    "Debug",
                    $"Found {list.Count} components in {obj.name} and copied them to clipboard",
                    "Ok"
                );
            });
        }

        [MenuItem(reserialize, priority = reserializePriority)]
        private static void Reserialize() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                var doIt = EditorUtility.DisplayDialog(
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

                EditorUtility.DisplayDialog(
                    "VRCFury",
                    $"Done",
                    "Ok"
                );
            });
        }
    }
}
