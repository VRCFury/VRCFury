using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class SaveAssetsService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;
        private readonly Lazy<SaveAssetsSession> session;

        public SaveAssetsService() {
            session = new Lazy<SaveAssetsSession>(() => {
                var outputDirName = originalAvatarService.GetOriginalName() ?? avatarObject.name;
                return new SaveAssetsSession(outputDirName);
            });
        }

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            Run(controllers.GetAllUsedControllers());
        }

        public SaveAssetsSession Session => session.Value;

        public void Run(IEnumerable<VFAvatarController> controllersToSave) {
            var controllersToSaveArray = controllersToSave.ToArray();
            foreach (var controller in controllersToSaveArray) {
                controller.name = $"VRCFury {controller.GetType()}";
                var saved = controller.Save(avatarObject, Session);
                VRCAvatarUtils.SetAvatarController(avatar, controller.GetType(), saved);
            }
            if (controllersToSaveArray.Any(controller => controller == controllers.GetFx())) {
                var rootAnimator = avatarObject.GetComponent<Animator>();
                if (rootAnimator != null && rootAnimator.runtimeAnimatorController != null) {
                    rootAnimator.runtimeAnimatorController = VRCAvatarUtils.GetAvatarController(avatar, controllers.GetFx().GetType()).Item2;
                }
            }

            NameMenuAssets(VRCAvatarUtils.GetAvatarMenu(avatar));

            // Save mats and meshes
            Session.SaveAssetAndChildren(avatar);
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                Session.SaveAssetAndChildren(component);
            }
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<MeshFilter>()) {
                Session.SaveAssetAndChildren(component);
            }
            foreach (var audioSource in avatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                Session.SaveAssetAndChildren(audioSource);
            }
            // Generated Avatars on generic rigs
            foreach (var animator in avatarObject.GetComponentsInSelfAndChildren<Animator>()) {
                Session.SaveAssetAndChildren(animator);
            }

            Session.Finish();
        }

        internal static void NameMenuAssets(VRCExpressionsMenu root) {
            if (root == null) return;
            root.ForEachMenu(
                (menu, path) => {
                    if (!VrcfObjectFactory.DidCreate(menu) || EditorUtility.IsPersistent(menu)) return;
                    menu.name = path.Count == 0 ? "VRCFury Menu" : string.Join(
                        " » ",
                        path.Select(name => {
                            name = Regex.Replace(name, "<.*?>", "");
                            name = Regex.Replace(name, "\n.*", "");
                            return Regex.Replace(name, " +", " ").Trim();
                        })
                    );
                },
                dirtyAll: false
            );
        }
    }
}
