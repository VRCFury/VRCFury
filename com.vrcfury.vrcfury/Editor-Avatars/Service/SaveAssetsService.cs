using UnityEngine;
using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            // This works without WithoutAssetEditing in <2022, but in unity 6+, saving an asset during AssetEditing
            // causes the Asset Path to never show up until AssetEditing is ended, which breaks NeedsSaved and
            // AttachAsset
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                var tmpDir = tmpDirService.GetTempDir();
                var session = new SaveAssetsSession();

                // Save mats and meshes
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                    session.SaveUnsavedComponentAssets(component, tmpDir);
                }

                foreach (var controller in controllers.GetAllMutatedControllers()) {
                    var raw = VRCAvatarUtils.GetAvatarController(avatar, controller.GetType()).Item2 as AnimatorController;
                    if (raw == null) {
                        continue;
                    }
                    session.SaveAssetAndChildren(
                        raw,
                        $"VRCFury {controller.GetType().ToString()}",
                        tmpDir
                    );
                }

                // Save everything else
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    session.SaveUnsavedComponentAssets(component, tmpDir);
                }

                session.FlushWorkLogManifest(tmpDir);
            });
        }
    }
}
