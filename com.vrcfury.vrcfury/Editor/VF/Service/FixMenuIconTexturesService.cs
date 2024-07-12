using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
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
        [VFAutowired] private readonly AvatarManager manager;

        [FeatureBuilderAction(FeatureOrder.FixMenuIconTextures)]
        public void Apply() {
            var menu = manager.GetMenu();
            
            var cache = new Dictionary<Texture2D, Texture2D>();
            Texture2D Optimize(Texture2D original, string path) {
                if (original == null) return original;
                if (original.width == 0 || original.height == 0) {
                    EditorUtility.DisplayDialog(
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
                return cache[original] = newTexture;
            }
            
            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                var strPath = string.Join("/", path);
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
