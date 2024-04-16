using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Utils;

namespace VF.Service {
    [VFService]
    public class ArmatureLinkHelperService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => globals.avatarObject;
        
        private readonly Dictionary<VFGameObject, string> originalNames
            = new Dictionary<VFGameObject, string>();
        private readonly HashSet<VFGameObject> availableForCleanup
            = new HashSet<VFGameObject>();
        private readonly VFMultimapSet<VFGameObject, VFGameObject> animLink
            = new VFMultimapSet<VFGameObject, VFGameObject>();

        public string GetOriginalName(VFGameObject obj) {
            return originalNames.TryGetValue(obj, out var orig) ? orig : obj.name;
        }

        public void SetOriginalName(VFGameObject obj, string name) {
            originalNames[obj] = name;
        }

        public void MarkAvailableForCleanup(VFGameObject obj) {
            availableForCleanup.Add(obj);
        }

        public void LinkEnableAnims(VFGameObject from, VFGameObject to) {
            animLink.Put(from, to);
        }

        [FeatureBuilderAction(FeatureOrder.FinalizeArmatureLinks)]
        public void FinalizeArmatureLinks() {
            // Clean up objects that don't need to exist anymore
            // (this should happen before toggle rewrites, so we don't have to add toggles for a ton of things that won't exist anymore)
            var usedReasons = ObjectUsageService.GetUsageReasons(avatarObject);

            var tryToCleanup = availableForCleanup.AllChildren().ToArray();

            foreach (var obj in tryToCleanup) {
                if (obj == null) continue;
                if (!usedReasons.ContainsKey(obj)) obj.Destroy();
            }

            // Rewrite animations that turn off parents
            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.type != typeof(GameObject)) continue;
                    var transform = avatarObject.Find(binding.path);
                    if (transform == null) continue;
                    foreach (var other in animLink.Get(transform)) {
                        if (other == null) continue; // it got deleted because the propBone wasn't used
                        var b = binding;
                        b.path = other.GetPath(avatarObject);
                        clip.SetFloatCurve(b, clip.GetFloatCurve(binding));
                    }
                }
            }

            if (Application.isPlaying) {
                foreach (var obj in tryToCleanup) {
                    if (obj == null) continue;
                    if (usedReasons.ContainsKey(obj)) {
                        var debugInfo = obj.AddComponent<VRCFuryDebugInfo>();
                        debugInfo.debugInfo =
                            "VRCFury Armature Link did not clean up this object because it is still used:\n";
                        debugInfo.debugInfo += string.Join("\n", usedReasons.Get(obj).OrderBy(a => a));
                    }
                }
            }
        }
    }
}
