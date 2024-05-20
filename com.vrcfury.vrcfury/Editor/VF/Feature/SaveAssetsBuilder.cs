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
    public class SaveAssetsBuilder {
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
                    $"VRCFury {controller.GetType().ToString()} for {manager.AvatarObject.name}"
                );
            }

            // Save everything else
            foreach (var component in manager.AvatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                foreach (var asset in GetUnsavedChildren(component, recurse: false)) {
                    SaveAssetAndChildren(
                        asset,
                        $"VRCFury {asset.GetType().Name} for {component.owner().name}"
                    );
                }
            }
        }

        private IList<Object> GetUnsavedChildren(Object obj, bool recurse = true) {
            var unsavedChildren = new List<Object>();
            var clipReplacements = new Dictionary<Object, Object>();
            MutableManager.ForEachChild(obj, asset => {
                if (asset == obj) return true;
                if (obj is MonoBehaviour m && MonoScript.FromMonoBehaviour(m) == asset) return false;
                if (IsSaved(asset)) return false;
                if (asset is AnimationClip vac) {
                    var proxyClip = vac.GetProxyClip();
                    if (proxyClip != null) {
                        clipReplacements[vac] = proxyClip;
                        return false;
                    }
                    vac.FinalizeAsset();
                }
                unsavedChildren.Add(asset);
                return recurse;
            });
            if (clipReplacements.Count > 0) {
                foreach (var o in unsavedChildren) {
                    MutableManager.RewriteInternals(o, clipReplacements);
                }
            }
            return unsavedChildren;
        }

        private static bool IsSaved(Object asset) {
            // Note to future self. DO NOT RETURN TRUE FOR MonoScript HERE
            // State machine behaviors are MonoScripts.
            if (asset is UnityEngine.Component || asset is GameObject) return true;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;
            return false;
        }

        private void SaveAssetAndChildren(Object asset, string filename) {
            if (IsSaved(asset)) return;

            var unsavedChildren = GetUnsavedChildren(asset);

            // Save child textures
            // If we don't save textures before the materials that use them, unity just throws them away
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) {
                    VRCFuryAssetDatabase.SaveAsset(subAsset, manager.tmpDir, filename + "_" + subAsset.name);
                }
            }
            
            // Save the main asset
            VRCFuryAssetDatabase.SaveAsset(asset, manager.tmpDir, filename);

            // Attach children
            foreach (var subAsset in unsavedChildren) {
                if (subAsset is Texture2D) continue;
                AssetDatabase.RemoveObjectFromAsset(subAsset);
                AssetDatabase.AddObjectToAsset(subAsset, asset);
            }
        }
    }
}
