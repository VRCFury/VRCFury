using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
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
                ForEachUnsavedChild(component, asset => {
                    SaveAssetAndChildren(
                        asset,
                        $"VRCFury {asset.GetType().Name} for {component.gameObject.name}"
                    );
                    return false;
                });
            }
        }

        private void ForEachUnsavedChild(Object obj, Func<Object, bool> visit) {
            MutableManager.ForEachChild(obj, asset => {
                if (asset == obj) return true;
                if (IsSaved(asset)) return false;
                return visit(asset);
            });
        }

        private static bool IsSaved(Object asset) {
            if (asset is UnityEngine.Component || asset is GameObject || asset is MonoScript) return true;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;
            return false;
        }

        private void SaveAssetAndChildren(Object asset, string filename) {
            if (IsSaved(asset)) return;

            // Save child textures
            // If we don't save textures before the materials that use them, unity just throws them away
            ForEachUnsavedChild(asset, subAsset => {
                if (subAsset is Texture2D) {
                    VRCFuryAssetDatabase.SaveAsset(subAsset, manager.tmpDir, filename + "_" + subAsset.name);
                }
                return true;
            });
            
            // Save the main asset
            VRCFuryAssetDatabase.SaveAsset(asset, manager.tmpDir, filename);

            // Attach children
            ForEachUnsavedChild(asset, subAsset => {
                AssetDatabase.AddObjectToAsset(subAsset, asset);
                return true;
            });
        }
    }
}
