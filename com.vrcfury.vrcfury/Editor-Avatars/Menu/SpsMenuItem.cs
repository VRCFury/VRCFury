using UnityEditor;
using VF.Builder.Haptics;
using VF.Exceptions;

namespace VF.Menu {
    internal static class SpsMenuItem {
        [MenuItem(MenuItems.upgradeLegacyHaptics, priority = MenuItems.upgradeLegacyHapticsPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                SpsUpgrader.Run();
            });
        }

        [MenuItem(MenuItems.upgradeLegacyHaptics, true)]
        private static bool Check() {
            return SpsUpgrader.Check();
        }

        [MenuItem("GameObject/VRCFury/Create SPS Socket", priority = 40)]
        [MenuItem(MenuItems.createSocket, priority = MenuItems.createSocketPriority)]
        public static void RunSocket() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                HapticsMenuItem.Create(false);
            });
        }

        [MenuItem("GameObject/VRCFury/Create SPS Plug", priority = 41)]
        [MenuItem(MenuItems.createPlug, priority = MenuItems.createPlugPriority)]
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

        [MenuItem(MenuItems.nukeZawoo, priority = MenuItems.nukeZawooPriority)]
        private static void NukeZawooParts() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                ZawooDeleter.Run(MenuUtils.GetSelectedAvatar());
            });
        }

        [MenuItem(MenuItems.nukeZawoo, true)]
        private static bool CheckNukeZawooParts() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
    }
}