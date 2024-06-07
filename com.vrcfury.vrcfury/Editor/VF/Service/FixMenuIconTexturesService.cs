using System.Collections.Generic;
using System.Linq;
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
            Texture2D Optimize(Texture2D original) {
                if (original == null) return original;
                if (cache.TryGetValue(original, out var cached)) return cached;
                var newTexture = original.Optimize(forceCompression: true, maxSize: 256);
                if (original != newTexture) {
                    Debug.LogWarning($"VRCFury is resizing/compressing menu icon {original.name}");
                }
                return cache[original] = newTexture;
            }
            
            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                control.icon = Optimize(control.icon);
                if (control.labels != null) {
                    control.labels = control.labels.Select(label => {
                        label.icon = Optimize(label.icon);
                        return label;
                    }).ToArray();
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
        }
    }
}
