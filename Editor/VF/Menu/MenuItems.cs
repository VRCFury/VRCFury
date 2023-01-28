using UnityEditor;

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
        
        [MenuItem("Tools/VRCFury/OseGB", priority = 1200)]
        private static void MarkerOseGB() {
        }

        [MenuItem("Tools/VRCFury/OseGB", true)]
        private static bool MarkerOseGB2() {
            return false;
        }

        [MenuItem("Tools/VRCFury/Upgrade avatar for OscGB", priority = 1201)]
        private static void Run() {
            DPSContactUpgradeBuilder.Run();
        }

        [MenuItem("Tools/VRCFury/Upgrade avatar for OscGB", true)]
        private static bool Check() {
            return DPSContactUpgradeBuilder.Check();
        }
        
        [MenuItem("Tools/VRCFury/Create Orifice", priority = 1202)]
        public static void RunHole() {
            OrificeCreatorMenuItem.Create();
        }

        [MenuItem("Tools/VRCFury/Migrate Parent-Constraint Orifices to OGB (DPSAttach, __dps)", priority = 1204)]
        private static void RunMigrateDPSAttach() {
            DpsAttachMigration.Run(MenuUtils.GetSelectedAvatar());
        }
        
        [MenuItem("Tools/VRCFury/Migrate Parent-Constraint Orifices to OGB (DPSAttach, __dps)", true)]
        private static bool CheckMigrateDPSAttach() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
        
        [MenuItem("Tools/VRCFury/Bake OscGB Component", priority = 1205)]
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
            ZawooDeleter.Run(MenuUtils.GetSelectedAvatar());
        }
        
        [MenuItem("Tools/VRCFury/Nuke Zawoo Parts", true)]
        private static bool CheckNukeZawooParts() {
            return MenuUtils.GetSelectedAvatar() != null;
        }

        [MenuItem("Tools/VRCFury/Build a Test Copy", priority = 1402)]
        private static void RunForceRun() {
            VRCFuryTestCopyMenuItem.RunBuildTestCopy();
        }

        [MenuItem("Tools/VRCFury/Build a Test Copy", true)]
        private static bool CheckForceRun() {
            return VRCFuryTestCopyMenuItem.CheckBuildTestCopy();
        }
    }
}
