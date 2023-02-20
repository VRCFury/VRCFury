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
            
            if (Directory.Exists(tmpDirParent)) {
                foreach (var asset in AssetDatabase.FindAssets("", new[] { tmpDirParent })) {
                    var path = AssetDatabase.GUIDToAssetPath(asset);
                    AssetDatabase.DeleteAsset(path);
                }
            }
            Directory.CreateDirectory(tmpDir);
        }

        /** Removes VRCF from avatars made in the pre-"nondestructive" days */
        private void CleanFromAvatar(GameObject a) {
            AvatarCleaner.Cleanup(avatarObject, perform: true,
                ShouldRemoveAsset: VRCFuryAssetDatabase.IsVrcfAsset,
                ShouldRemoveLayer: name => name.StartsWith("[VRCFury]"),
                ShouldRemoveParam: s => s.StartsWith("Senky") || s.StartsWith("VRCFury__")
            );
        }
    }
}
