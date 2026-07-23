using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            Run(controllers.GetAllUsedControllers());
        }

        public void Run(IEnumerable<ControllerManager> controllersToSave) {
            var controllersToSaveArray = controllersToSave.ToArray();
            foreach (var controller in controllersToSaveArray) {
                controller.parameters = controller.parameters
                    .OrderBy(p => p.name)
                    .ToArray();
                var saved = controller.Save(
                    avatarObject,
                    tmpDirService.GetTempDir(),
                    $"VRCFury {controller.GetType()}"
                );
                VRCAvatarUtils.SetAvatarController(avatar, controller.GetType(), saved);
            }
            if (controllersToSaveArray.Any(controller => controller == controllers.GetFx())) {
                var rootAnimator = avatarObject.GetComponent<Animator>();
                if (rootAnimator != null && rootAnimator.runtimeAnimatorController != null) {
                    rootAnimator.runtimeAnimatorController = VRCAvatarUtils.GetAvatarController(avatar, controllers.GetFx().GetType()).Item2;
                }
            }

            var tmpDir = tmpDirService.GetTempDir();
            var session = new SaveAssetsSession();
            BinaryContainer otherAssetsParent = null;
            BinaryContainer GetOtherAssetsParent() {
                if (otherAssetsParent == null) {
                    otherAssetsParent = VrcfObjectFactory.Create<BinaryContainer>();
                    otherAssetsParent.name = "Other";
                    session.SaveAssetAndChildren(otherAssetsParent, "VRCFury Other", tmpDir);
                }
                return otherAssetsParent;
            }

            // Save mats and meshes
            session.SaveUnsavedComponentAssets(avatar, tmpDir, GetOtherAssetsParent);
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                session.SaveUnsavedComponentAssets(component, tmpDir, GetOtherAssetsParent);
            }
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<MeshFilter>()) {
                session.SaveUnsavedComponentAssets(component, tmpDir, GetOtherAssetsParent);
            }
            foreach (var audioSource in avatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                session.SaveUnsavedComponentAssets(audioSource, tmpDir, GetOtherAssetsParent);
            }

            SaveAssetsSession.FlushWorkLogManifest(tmpDir);
        }
    }
}
