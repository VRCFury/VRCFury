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
        
        [MenuItem("Tools/VRCFury/Create Hole", priority = 1202)]
        public static void RunHole() {
            OrificeCreatorMenuItem.RunHole();
        }
        
        [MenuItem("Tools/VRCFury/Create Ring", priority = 1203)]
        public static void RunRing() {
            OrificeCreatorMenuItem.RunRing();
        }
        
        [MenuItem("Tools/VRCFury/Manually mark mesh as penetrator", priority = 1204)]
        public static void RunManualPen() {
            OrificeCreatorMenuItem.RunPen();
        }
        
        [MenuItem("Tools/VRCFury/Remove OscGB from avatar", priority = 1205)]
        private static void RunOGBRemove() {
            DPSContactUpgradeBuilder.Remove();
        }

        [MenuItem("Tools/VRCFury/Remove OscGB from avatar", true)]
        private static bool RunOGBRemoveCheck() {
            return DPSContactUpgradeBuilder.Check();
        }

        //
        
        [MenuItem("Tools/VRCFury/Debug", priority = 1400)]
        private static void MarkerDebug() {
        }

        [MenuItem("Tools/VRCFury/Debug", true)]
        private static bool MarkerDebug2() {
            return false;
        }
        
        [MenuItem("Tools/VRCFury/Build Test Avatar", priority = 1401)]
        private static void RunForceRun() {
            VRCFuryForceRunMenuItem.RunFakeUpload();
        }

        [MenuItem("Tools/VRCFury/Build Test Avatar", true)]
        private static bool CheckForceRun() {
            return VRCFuryForceRunMenuItem.CheckFakeUpload();
        }
        
        [MenuItem("Tools/VRCFury/Purge from Selected Avatar", priority = 1402)]
        private static void RunPurge() {
            VRCFuryForceRunMenuItem.RunPurge();
        }

        [MenuItem("Tools/VRCFury/Purge from Selected Avatar", true)]
        private static bool CheckPurge() {
            return VRCFuryForceRunMenuItem.CheckPurge();
        }
    }
}
