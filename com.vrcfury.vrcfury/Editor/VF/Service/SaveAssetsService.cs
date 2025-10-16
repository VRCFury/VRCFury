using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

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

                // Save mats and meshes
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                    SaveUnsavedComponentAssets(component, tmpDir);
                }

                // Special handling for mask and controller names
                foreach (var controller in controllers.GetAllMutatedControllers()) {
                    foreach (var layer in controller.GetLayers()) {
                        if (layer.mask != null) {
                            layer.mask.name = "Mask for " + layer.name;
                        }
                    }

                    SaveAssetAndChildren(
                        controller.GetRaw(),
                        $"VRCFury {controller.GetType().ToString()}",
                        tmpDir,
                        true
                    );
                }

                // Save everything else
                foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    SaveUnsavedComponentAssets(component, tmpDir);
                }
            });
        }

        public static void SaveUnsavedComponentAssets(UnityEngine.Component component, string tmpDir) {
            foreach (var asset in GetUnsavedChildren(component, false, true)) {
                string filename;
                if (asset is VRCExpressionsMenu) {
                    filename = "VRCFury Menu";
                } else if (asset is VRCExpressionParameters) {
                    filename = "VRCFury Params";
                } else {
                    filename = $"VRCFury {asset.name} - {component.owner().name}";
                }
                SaveAssetAndChildren(
                    asset,
                    filename,
                    tmpDir,
                    true
                );
            }
        }

        private static IList<Object> GetUnsavedChildren(Object obj, bool recurse, bool reuseOriginalClips) {
            var unsavedChildren = new List<Object>();
            var clipReplacements = new Dictionary<Object, Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset == obj) return true;
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!VrcfObjectFactory.DidCreate(asset)) return false;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;
                if (asset is AnimationClip vac) {
                    if (reuseOriginalClips) {
                        var useOriginalClip = vac.GetUseOriginalUserClip();
                        if (useOriginalClip != null) {
                            clipReplacements[vac] = useOriginalClip;
                            return false;
                        }
                    }
                    vac.FinalizeAsset();
                }
                unsavedChildren.Add(asset);
                return recurse;
            });
            if (clipReplacements.Count > 0) {
                foreach (var o in unsavedChildren) {
                    if (o is AnimationClip) continue;
                    MutableManager.RewriteInternals(o, clipReplacements);
                }
            }
            return unsavedChildren;
        }

        public static void SaveAssetAndChildren(Object asset, string filename, string tmpDir, bool reuseOriginalClips) {
            if (!VrcfObjectFactory.DidCreate(asset)) return;

            var unsavedChildren = GetUnsavedChildren(asset, true, reuseOriginalClips);

            // Save child textures
            // If we don't save textures before the materials that use them, unity just throws them away
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) {
                    VRCFuryAssetDatabase.SaveAsset(subAsset, tmpDir, filename + "_" + subAsset.name);
                }
            }
            
            // Save the main asset
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) {
                VRCFuryAssetDatabase.SaveAsset(asset, tmpDir, filename);
            }

            // Attach children
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) continue;
                if (subAsset is AnimatorStateMachine
                    || subAsset is AnimatorState
                    || subAsset is AnimatorTransitionBase
                    || subAsset is StateMachineBehaviour
                    || subAsset is BlendTree
                ) {
                    subAsset.hideFlags |= HideFlags.HideInHierarchy;
                }

                VRCFuryAssetDatabase.AttachAsset(subAsset, asset);
            }
        }
    }
}
