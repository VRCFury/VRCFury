using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VF.Utils {
    internal static class AlcomUtils {
        private static readonly Lazy<bool> isInstalled = new Lazy<bool>(DetectInstallation);

        public static bool IsInstalled() {
            return isInstalled.Value;
        }

        public static string GetName() {
            return IsInstalled() ? "ALCOM" : "the VRChat Creator Companion";
        }

        private static bool DetectInstallation() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return !string.IsNullOrEmpty(localAppData)
                   && File.Exists(Path.Combine(localAppData, "Programs", "ALCOM", "ALCOM.exe"));
        }
    }
}
