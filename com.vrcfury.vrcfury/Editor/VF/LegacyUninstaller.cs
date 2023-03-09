using System.IO;
using UnityEditor;

namespace VF {
    [InitializeOnLoad]
    public class LegacyUninstaller {
        static LegacyUninstaller() {
            // GUID of old Assets/VRCFury folder
            var removed = false;
            var legacyDir = AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae");
            if (!string.IsNullOrWhiteSpace(legacyDir)) {
                removed = true;
                AssetDatabase.DeleteAsset(legacyDir);
            }
            if (Directory.Exists("Assets/VRCFury")) {
                removed = true;
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
