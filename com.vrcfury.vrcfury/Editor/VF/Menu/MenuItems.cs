using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Model;
using VF.Model.Feature;

namespace VF.Menu {
    public class MenuItems {
        private const string prefix = "Tools/VRCFury/";

        public const string testCopy = prefix + "Build an Editor Test Copy";
        public const int testCopyPriority = 1221;
        public const string playMode = prefix + "Build during play mode";
        public const int playModePriority = 1222;
        public const string autoUpload = prefix + "Skip VRChat upload screen";
        public const int autoUploadPriority = 1223;
        
        public const string createSocket = prefix + "Haptics/Create Socket";
        public const int createSocketPriority = 1301;
        public const string createPlug = prefix + "Haptics/Create Plug";
        public const int createPlugPriority = 1302;
        public const string upgradeLegacyHaptics = prefix + "Haptics/Upgrade legacy haptics";
        public const int upgradeLegacyHapticsPriority = 1303;
        public const string bakeHaptic = prefix + "Haptics/Bake Haptic Component";
        public const int bakeHapticPriority = 1304;

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

        [MenuItem(upgradeLegacyHaptics, priority = upgradeLegacyHapticsPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                LegacyHapticsUpgrader.Run();
            });
        }

        [MenuItem(upgradeLegacyHaptics, true)]
        private static bool Check() {
            return LegacyHapticsUpgrader.Check();
        }
        
        [MenuItem(createSocket, priority = createSocketPriority)]
        public static void RunSocket() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                HapticsMenuItem.Create(false);
            });
        }
        
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

        [MenuItem(listComponents, priority = listComponentsPriority)]
        private static void ListChildComponents() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                VFGameObject obj = Selection.activeGameObject;
                if (obj == null) return;
                var list = new List<string>();
                foreach (var c in obj.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    if (c == null || c is Transform) continue;
                    list.Add(c.GetType().Name + " in " + c.owner().GetPath(obj));
                }

                Debug.Log($"List of components on {obj}:\n" + string.Join("\n", list));

                EditorUtility.DisplayDialog(
                    "Debug",
                    $"Found {list.Count} components in {obj.name} and logged them to the console",
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
