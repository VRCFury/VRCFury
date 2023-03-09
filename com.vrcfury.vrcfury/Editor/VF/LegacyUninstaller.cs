using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF {
    [InitializeOnLoad]
    public class LegacyUninstaller {
        static LegacyUninstaller() {
            // GUID of old Assets/VRCFury folder
            var removed = false;
            var legacyDir = AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae");
            if (!string.IsNullOrWhiteSpace(legacyDir) && Directory.Exists(legacyDir)) {
                removed = true;
                Debug.Log($"VRCFury found a legacy install at location: {legacyDir}");
                AssetDatabase.DeleteAsset(legacyDir);
            }
            if (Directory.Exists("Assets/VRCFury")) {
                removed = true;
                Debug.Log($"VRCFury found a legacy install at location: Assets/VRCFury");
                AssetDatabase.DeleteAsset("Assets/VRCFury");
            }

            if (removed) {
                EditorUtility.DisplayDialog(
                    "VRCFury Updater",
                    "VRCFury has moved from Assets/VRCFury to Packages/VRCFury*. Don't worry, it's still there!",
                    "Ok"
                );
            }
        }
    }
}
