using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Menu;

namespace VF.Feature {
    public class CleanupLegacyBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupLegacy)]
        public void Apply() {
            if (originalObject) CleanFromAvatar(originalObject);
            CleanFromAvatar(avatarObject);

            VRCFuryAssetDatabase.DeleteFolder(tmpDirParent);
            Directory.CreateDirectory(tmpDir);
        }

        /** Removes VRCF from avatars made in the pre-"nondestructive" days */
        private void CleanFromAvatar(GameObject a) {
            AvatarCleaner.Cleanup(avatarObject, perform: true,
                ShouldRemoveAsset: obj => {
                    if (obj == null) return false;
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (path == null) return false;
                    return path.Contains("_VRCFury/");
                },
                ShouldRemoveLayer: name => name.StartsWith("[VRCFury]"),
                ShouldRemoveParam: s => s.StartsWith("Senky") || s.StartsWith("VRCFury__")
            );
        }
    }
}
