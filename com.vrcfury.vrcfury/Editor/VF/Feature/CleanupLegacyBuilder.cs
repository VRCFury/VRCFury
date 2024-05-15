using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Menu;
using Object = UnityEngine.Object;

namespace VF.Feature {
    public class CleanupLegacyBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupLegacy)]
        public void Apply() {
            CleanFromAvatar();

            VRCFuryAssetDatabase.DeleteFolder(tmpDirParent);
            VRCFuryAssetDatabase.CreateFolder(tmpDir);

#if UNITY_2022_1_OR_NEWER
            tempAsset = null;
#else
            tempAsset = new AnimatorController();
            VRCFuryAssetDatabase.SaveAsset(tempAsset, tmpDir, "tempStorage");
#endif
        }

        private static Object tempAsset;

        /**
         * Some unity calls blow up if the asset isn't saved. This saves them temporarily, then "un-saves" it,
         * along with whatever else the method added to the asset.
         */
        public static void WithTemporaryPersistence(Object obj, Action with) {
            if (tempAsset == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj))) {
                with();
                return;
            }

            AssetDatabase.AddObjectToAsset(obj, tempAsset);
            try {
                with();
            } finally {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(
                             AssetDatabase.GetAssetPath(tempAsset))) {
                    if (asset == tempAsset) continue;
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
            }
        }

        /** Removes VRCF from avatars made in the pre-"nondestructive" days */
        private void CleanFromAvatar() {
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
