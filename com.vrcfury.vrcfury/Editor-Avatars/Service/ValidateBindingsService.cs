using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * This service can determine if an animation binding is "valid" (can actually do something) or not
     */
    [VFService]
    internal class ValidateBindingsService {
        [CanBeNull] [VFAutowired] private readonly AnimatorHolderService animators;

        public bool HasValidBinding(VFMotion motion) {
            return new AnimatorIterator.Clips().From(motion).Any(HasValidBinding);
        }

        public bool HasValidBinding(VFClip clip) {
            return clip.GetAllBindings().Any(IsValid);
        }

        public bool IsValid(VFBinding binding) {
            var obj = binding.target;
            if (binding.type == null) return false;
            if (binding.type == typeof(Animator)) return true;

            // because we delete the animators during the build
            if (obj != null && binding.type.IsAssignableFrom(typeof(Animator))) {
                if (animators != null && animators.GetSubControllers().Any(s => s.owner == obj)) return true;
            }

            return AnimationBindingUtils.IsValidResolvedTarget(obj, binding.type);
        }
    }
}
