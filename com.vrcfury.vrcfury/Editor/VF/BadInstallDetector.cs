using System.IO;
using System.Linq;
using UnityEditor;
using VF.Updater;
using VF.Utils;

namespace VF {
    internal static class BadInstallDetector {
        [InitializeOnLoadMethod]
        private static void Init() {
            var isLocalPackage = Directory.Exists("Packages/com.vrcfury.vrcfury") &&
                                 Path.GetFullPath("Packages/com.vrcfury.vrcfury").StartsWith(Path.GetFullPath("Packages"));
            var manifestPath = "Packages/manifest.json";
            var manifestContainsVrcfury = File.Exists(manifestPath) && File.ReadLines(manifestPath)
                .Any(line => line.Contains("com.vrcfury.vrcfury"));

            if (isLocalPackage && manifestContainsVrcfury) {
                DialogUtils.DisplayDialog(
                    "VRCFury",
                    "The VRCFury install is partially corrupt. The updater may have broken, or you may have updated " +
                    "from an old manual install to a new version using the VCC.\n" +
                    "\n" +
                    "Please download and import " +
                    "https://vrcfury.com/installer to resolve this issue.",
                    "Ok"
                );
            }
        }
    }
}
