using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;

namespace VF.Menu {
    public class MenuItems {
        private const string prefix = "Tools/VRCFury/";

        public const string testCopy = prefix + "Build an Editor Test Copy";
        public const int testCopyPriority = 1221;
        public const string playMode = prefix + "Build during play mode";
        public const int playModePriority = 1222;
        public const string autoUpload = prefix + "Skip VRChat upload screen";
        public const int autoUploadPriority = 1223;
        
        public const string upgradeLegacyHaptics = prefix + "Haptics/Upgrade legacy haptics";
        public const int upgradeLegacyHapticsPriority = 1301;
        public const string createSocket = prefix + "Haptics/Create Socket";
        public const int createSocketPriority = 1302;
        public const string bakeHaptic = prefix + "Haptics/Bake Haptic Component";
        public const int bakeHapticPriority = 1303;

        public const string nukeZawoo = prefix + "Utilites/Nuke Zawoo";
        public const int nukeZawooPriority = 1311;
        public const string unusedBones = prefix + "Utilites/Nuke unused bones";
        public const int unusedBonesPriority = 1312;
        public const string listComponents = prefix + "Utilites/List All Components";
        public const int listComponentsPriority = 1313;
        public const string detectDuplicatePhysbones = prefix + "Utilites/Detect Duplicate Physbones";
        public const int detectDuplicatePhysbonesPriority = 1314;

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
        public static void RunHole() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                HapticsMenuItem.Create();
            });
        }

        [MenuItem(bakeHaptic, priority = bakeHapticPriority)]
        public static void RunBake() {
            HapticsMenuItem.RunBake();
        }

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
                var obj = Selection.activeGameObject;
                if (obj == null) return;
                var list = new List<string>();
                foreach (var c in obj.GetComponentsInChildren<UnityEngine.Component>(true)) {
                    if (c == null || c is Transform) continue;
                    list.Add(c.GetType().Name + " in " + AnimationUtility.CalculateTransformPath(c.transform, obj.transform));
                }

                EditorUtility.DisplayDialog(
                    "Debug",
                    string.Join("\n", list),
                    "Ok"
                );
            });
        }
    }
}
