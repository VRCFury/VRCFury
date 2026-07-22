using System.Linq;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * This service can determine if an animation binding is "valid" (can actually do something) or not
     */
    [VFService]
    internal class ValidateBindingsService {
        public bool HasValidBinding(VFMotion motion) {
            return new AnimatorIterator.Clips().From(motion).Any(HasValidBinding);
        }

        public bool HasValidBinding(VFClip clip) {
            return clip.GetAllBindings().Any(IsValid);
        }

        public bool IsValid(VFBinding binding) {
            var obj = binding.target;
            if (binding.type == null) return false;
            if (binding.IsAnimatorBinding()) return true;

            return AnimationBindingUtils.IsValidResolvedTarget(obj, binding.type);
        }
    }
}
