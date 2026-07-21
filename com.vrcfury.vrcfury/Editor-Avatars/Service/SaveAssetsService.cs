using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            var tmpDir = tmpDirService.GetTempDir();
            var session = new SaveAssetsSession();

            // Save mats and meshes
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                session.SaveUnsavedComponentAssets(component, tmpDir);
            }

            // Save everything else
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                session.SaveUnsavedComponentAssets(component, tmpDir);
            }

            session.FlushWorkLogManifest(tmpDir);
        }
    }
}
