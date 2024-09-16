using System;
using UnityEditor;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {
    [VFService]
    internal class CleanupLegacyService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;

        [FeatureBuilderAction(FeatureOrder.CleanupLegacy)]
        public void Apply() {
            CleanFromAvatar();

            VRCFuryAssetDatabase.DeleteFolder(globals.tmpDirParent);
            VRCFuryAssetDatabase.CreateFolder(globals.tmpDir);

#if UNITY_2022_1_OR_NEWER
            tempAsset = null;
#else
            tempAsset = VrcfObjectFactory.Create<AnimatorController>();
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

            var hideFlags = obj.hideFlags;
            VRCFuryAssetDatabase.AttachAsset(obj, tempAsset);
            try {
                with();
            } finally {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(
                             AssetDatabase.GetAssetPath(tempAsset))) {
                    if (asset == tempAsset) continue;
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
                obj.hideFlags = hideFlags;
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
