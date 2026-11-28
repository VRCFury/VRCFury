using UnityEditor;

using VF.Utils;

namespace VF.Menu {
    internal static class TestCopyMenuItem {
        [VFInit]
        private static void InitHierarchyMenu() {
            MenuUtils.AddAvatarHierarchyCommand("Build an Editor Test Copy", VRCFuryTestCopyMenuItem.BuildTestCopy);
        }

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
