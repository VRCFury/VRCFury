using UnityEditor;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** Removes VRCF from avatars made in the pre-"nondestructive" days */
    [VFService]
    internal class CleanupLegacyService {
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.CleanupLegacy)]
        public void Apply() {
            CleanFromAvatar();
        }

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
