using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * The VRCSDK requires that all menu icons must be less than 256x256 and have compression enabled.
     * For icons added using vrcfury, we just automatically fix this for the user.
     */
    [VFService]
    internal class FixMenuIconTexturesService {
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        [FeatureBuilderAction(FeatureOrder.FixMenuIconTextures)]
        public void Apply() {
            var cache = new Dictionary<Texture2D, Texture2D>();
            Texture2D Optimize(Texture2D original, string path) {
                if (original == null) return original;
                if (original.width == 0 || original.height == 0) {
                    DialogUtils.DisplayDialog(
                        "VRCFury",
                        $"The icon in your menu at path \"{path}\" is corrupted." +
                        $" This is usually caused by a compression bug in Modular Avatar," +
                        $" where it does not compress non-standard image sizes properly. The icon has been discarded.",
                        "Ok"
                    );
                    return null;
                }
                if (cache.TryGetValue(original, out var cached)) return cached;
                var newTexture = original.Optimize(forceCompression: true, maxSize: 256);
                if (newTexture == original) {
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original)) as TextureImporter;
                    if (importer != null) {
                        // The SDK validates default importer settings, even when a platform override
                        // already produces a correctly sized and compressed texture.
                        var settings = importer.GetDefaultPlatformTextureSettings();
                        if (settings.textureCompression == TextureImporterCompression.Uncompressed
                            || ((original.width > 256 || original.height > 256) && settings.maxTextureSize > 256)) {
                            newTexture = original.Clone("Detached menu icon to satisfy VRCSDK default importer validation");
                        }
                    }
                }
                return cache[original] = newTexture;
            }
            
            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                var strPath = path.Join('/');
                control.icon = Optimize(control.icon, strPath);
                if (control.labels != null) {
                    control.labels = control.labels.Select(label => {
                        label.icon = Optimize(label.icon, strPath);
                        return label;
                    }).ToArray();
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
        }
    }
}
