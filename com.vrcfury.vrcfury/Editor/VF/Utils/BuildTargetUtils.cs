using UnityEditor;

namespace VF.Utils {
    internal static class BuildTargetUtils {
        public static bool IsDesktop() {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows
                   || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64
                   || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX
                   || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64;
        }
    }
}
