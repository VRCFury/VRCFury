using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    [VFService]
    internal class SaveAssetsBuilder {
        [VFAutowired] private readonly AvatarManager manager;

        [FeatureBuilderAction(FeatureOrder.SaveAssets)]
        public void Run() {
            // Special handling for mask and controller names
            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetLayers()) {
                    if (layer.mask != null) {
                        layer.mask.name = "Mask for " + layer.name;
                    }
                }
                SaveAssetAndChildren(
                    controller.GetRaw(),
                    $"VRCFury {controller.GetType().ToString()} for {manager.AvatarObject.name}",
                    manager.tmpDir
                );
            }

            // Save everything else
            foreach (var component in manager.AvatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                SaveUnsavedComponentAssets(component, manager.tmpDir);
            }
        }

        public static void SaveUnsavedComponentAssets(UnityEngine.Component component, string tmpDir) {
            foreach (var asset in GetUnsavedChildren(component, recurse: false)) {
                SaveAssetAndChildren(
                    asset,
                    $"VRCFury {asset.GetType().Name} for {component.owner().name}",
                    tmpDir
                );
            }
        }

        private static IList<Object> GetUnsavedChildren(Object obj, bool recurse = true) {
            var unsavedChildren = new List<Object>();
            var clipReplacements = new Dictionary<Object, Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset == obj) return true;
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (!NeedsSaved(asset)) return false;
                if (asset is AnimationClip vac) {
                    var useOriginalClip = vac.GetUseOriginalUserClip();
                    if (useOriginalClip != null) {
                        clipReplacements[vac] = useOriginalClip;
                        return false;
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

        private static bool NeedsSaved(Object asset) {
            return VrcfObjectFactory.DidCreate(asset) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));
        }

        private static void SaveAssetAndChildren(Object asset, string filename, string tmpDir) {
            if (!NeedsSaved(asset)) return;

            var unsavedChildren = GetUnsavedChildren(asset);

            // Save child textures
            // If we don't save textures before the materials that use them, unity just throws them away
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) {
                    subAsset.hideFlags = HideFlags.None;
                    VRCFuryAssetDatabase.SaveAsset(subAsset, tmpDir, filename + "_" + subAsset.name);
                }
            }
            
            // Save the main asset
            asset.hideFlags = HideFlags.None;
            VRCFuryAssetDatabase.SaveAsset(asset, tmpDir, filename);

            // Attach children
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) continue;
                subAsset.hideFlags = HideFlags.None;
                if (subAsset is AnimatorStateMachine
                    || subAsset is AnimatorState
                    || subAsset is AnimatorTransitionBase
                    || subAsset is StateMachineBehaviour
                    || subAsset is BlendTree
                ) {
                    subAsset.hideFlags |= HideFlags.HideInHierarchy;
                }

                AssetDatabase.RemoveObjectFromAsset(subAsset);
                AssetDatabase.AddObjectToAsset(subAsset, asset);
            }
        }
    }
}
