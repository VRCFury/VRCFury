using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF.Hooks {
    /**
     * Txl scripts blow up if player count is over 100. This raises the limit to 200.
     * https://github.com/vrctxl/CommonTXL/issues/4
     */
    internal static class FixTxlPlayerLimitHook {
        private const string OldText = "new VRCPlayerApi[100];";
        private const string NewText = "new VRCPlayerApi[200];";

        private static readonly string[] RelativePaths = {
            "Packages/com.texelsaur.playeraudio/Runtime/Scripts/AudioOverrideManager.cs",
            "Packages/com.texelsaur.playeraudio/Runtime/Scripts/AudioOverrideDebug.cs",
            "Packages/com.texelsaur.common/Runtime/Scripts/AccessControl.cs",
            "Packages/com.texelsaur.video/Runtime/Scripts/UI/PlayerControls.cs",
            "Packages/com.texelsaur.access/Runtime/Scripts/DebugUserList.cs"
        };

        [InitializeOnLoadMethod]
        private static void Init() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            var updated = 0;
            foreach (var relativePath in RelativePaths) {
                var fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
                if (!File.Exists(fullPath)) continue;

                var contents = File.ReadAllText(fullPath);
                if (!contents.Contains(OldText)) continue;

                File.WriteAllText(fullPath, contents.Replace(OldText, NewText));
                updated++;
            }

            if (updated > 0) {
                Debug.Log($"[VRCFury] FixTxlPlayerLimitHook updated {updated} TXL scripts to VRCPlayerApi[200].");
            }
        }
    }
}
