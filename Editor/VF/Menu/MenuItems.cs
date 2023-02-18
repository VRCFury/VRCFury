using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Menu {
    public class MenuItems {
        
        //
        
        [MenuItem("Tools/VRCFury/Update", priority = 1000)]
        private static void MarkerUpdate() {
        }

        [MenuItem("Tools/VRCFury/Update", true)]
        private static bool MarkerUpdate2() {
            return false;
        }

        [MenuItem("Tools/VRCFury/Update VRCFury", priority = 1001)]
        private static void Upgrade() {
            VRCFuryUpdater.Upgrade();
        }
        
        //
        
        [MenuItem("Tools/VRCFury/OGB", priority = 1200)]
        private static void MarkerOGB() {
        }

        [MenuItem("Tools/VRCFury/OGB", true)]
        private static bool MarkerOGB2() {
            return false;
        }

        [MenuItem("Tools/VRCFury/Upgrade avatar for OGB", priority = 1201)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                OGBUpgradeMenuItem.Run();
            });
        }

        [MenuItem("Tools/VRCFury/Upgrade avatar for OGB", true)]
        private static bool Check() {
            return OGBUpgradeMenuItem.Check();
        }
        
        [MenuItem("Tools/VRCFury/Create Orifice", priority = 1202)]
        public static void RunHole() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                OrificeCreatorMenuItem.Create();
            });
        }

        [MenuItem("Tools/VRCFury/Bake OGB Component", priority = 1205)]
        public static void RunBake() {
            OrificeCreatorMenuItem.RunBake();
        }
        
        //

        [MenuItem("Tools/VRCFury/Debug", priority = 1400)]
        private static void MarkerDebug() {
        }

        [MenuItem("Tools/VRCFury/Debug", true)]
        private static bool MarkerDebug2() {
            return false;
        }
        
        [MenuItem("Tools/VRCFury/Nuke Zawoo Parts", priority = 1401)]
        private static void NukeZawooParts() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                ZawooDeleter.Run(MenuUtils.GetSelectedAvatar());
            });
        }
        
        [MenuItem("Tools/VRCFury/Nuke Zawoo Parts", true)]
        private static bool CheckNukeZawooParts() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
        
        public const string unusedBones_name = "Tools/VRCFury/Nuke unused bones";
        public const int unusedBones_priority = 1402;

        public const string testCopy_name = "Tools/VRCFury/Build an Editor Test Copy";
        public const int testCopy_priority = 1403;
        [MenuItem(testCopy_name, priority = testCopy_priority)]
        private static void RunForceRun() {
            VRCFuryTestCopyMenuItem.RunBuildTestCopy();
        }
        [MenuItem(testCopy_name, true)]
        private static bool CheckForceRun() {
            return VRCFuryTestCopyMenuItem.CheckBuildTestCopy();
        }

        public const string playMode_name = "Tools/VRCFury/Build during play mode";
        public const int playMode_priority = 1404;
        
        public const string autoUpload_name = "Tools/VRCFury/Skip VRChat upload screen";
        public const int autoUpload_priority = 1405;

        /*
        [MenuItem("Tools/VRCFury/List All Components", priority = 1403)]
        private static void ListChildComponents() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                var obj = Selection.activeGameObject;
                if (obj == null) return;
                var list = new List<string>();
                foreach (var c in obj.GetComponentsInChildren<Component>(true)) {
                    if (c is Transform) continue;
                    list.Add(c.GetType().Name + " in " + AnimationUtility.CalculateTransformPath(c.transform, obj.transform));
                }

                EditorUtility.DisplayDialog(
                    "Debug",
                    string.Join("\n", list),
                    "Ok"
                );
            });
        }
        */
    }
}
