using System.Collections.Generic;
using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    [VFService]
    public class FixAmbiguousObjectNamesBuilder {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => globals.avatarObject;

        private readonly Dictionary<string, string> movedPaths
            = new Dictionary<string, string>();

        [FeatureBuilderAction(FeatureOrder.FixAmbiguousObjectNames)]
        public void Apply() {
            var oldPaths = new Dictionary<VFGameObject, string>();
            foreach (var obj in avatarObject.GetSelfAndAllChildren()) {
                oldPaths[obj] = obj.GetPath(avatarObject);
            }

            foreach (var obj in avatarObject.GetSelfAndAllChildren()) {
                if (obj == avatarObject) continue;
                obj.EnsureAnimationSafeName();
            }

            foreach (var (obj,oldPath) in oldPaths) {
                var newPath = obj.GetPath(avatarObject);
                movedPaths.TryAdd(oldPath, newPath);
            }
        }

        [FeatureBuilderAction(FeatureOrder.FixAmbiguousAnimations)]
        public void FixAnimations() {
            var rewriter = AnimationRewriter.RewritePath(path =>
                movedPaths.TryGetValue(path, out var to) ? to : path);

            foreach (var controller in manager.GetAllUsedControllers()) {
                ((AnimatorController)controller.GetRaw()).Rewrite(rewriter);
            }
        }
    }
}
