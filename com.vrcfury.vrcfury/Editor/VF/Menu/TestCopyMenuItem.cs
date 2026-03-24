using UnityEditor;

namespace VF.Menu {
    internal static class TestCopyMenuItem {
        [MenuItem(MenuItems.testCopy, priority = MenuItems.testCopyPriority)]
        private static void RunForceRun() {
            VRCFuryTestCopyMenuItem.RunBuildTestCopy();
        }
        [MenuItem(MenuItems.testCopy, true)]
        private static bool CheckForceRun() {
            return VRCFuryTestCopyMenuItem.CheckBuildTestCopy();
        }
    }
}
