using System.Collections.Generic;
using UnityEngine;
using VF.Utils;

namespace VF.Injector {
    internal static class VRCFuryPerFrameInjector {
        private static readonly Dictionary<VFGameObject, VRCFuryInjector> injectors
            = new Dictionary<VFGameObject, VRCFuryInjector>();

        public static VRCFuryInjector GetPerFrameInjector(VFGameObject obj) {
            Animator animator = null;
            for (var current = obj; current != null; current = current.parent) {
                foreach (var candidate in current.GetComponents<Animator>()) {
                    if (candidate != null && candidate.avatar != null) {
                        animator = candidate;
                    }
                }
            }
            var avatarObject = animator?.owner();
            var root = avatarObject ?? obj.root;
            return injectors.GetOrCreate(root, () => {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                if (avatarObject != null) injector.Set("avatarObject", avatarObject);
                return injector;
            });
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(injectors.Clear, 0);
        }
    }
}
