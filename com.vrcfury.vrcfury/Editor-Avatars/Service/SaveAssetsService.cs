using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;

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

                // Special handling for mask and controller names
                foreach (var controller in controllers.GetAllMutatedControllers()) {
                    foreach (var layer in controller.GetLayers()) {
                        if (layer.mask != null) {
                            layer.mask.name = "Mask for " + layer.name;
                        }
                    }

                    session.SaveAssetAndChildren(
                        controller.GetRaw(),
                        $"VRCFury {controller.GetType().ToString()}",
                        tmpDir,
                        true
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
