using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Validation.Performance;

namespace VF.Service {
    /**
     * This service can determine if an animation binding is "valid" (can actually do something) or not
     */
    [VFService]
    internal class ValidateBindingsService {
        private readonly VFGameObject baseObject;
        [CanBeNull] [VFAutowired] private readonly AnimatorHolderService animators;

        public ValidateBindingsService(VFGameObject avatarObject) {
            this.baseObject = avatarObject;
        }

        public bool HasValidBinding(Motion motion) {
            return new AnimatorIterator.Clips().From(motion).Any(HasValidBinding);
        }

        private bool HasValidBinding(AnimationClip clip) {
            return clip.GetAllBindings().Any(IsValid);
        }

        public bool IsValid(EditorCurveBinding binding) {
            var obj = baseObject.Find(binding.path);
            if (obj == null) return false;
            if (binding.type == null) return false;
            if (binding.type == typeof(GameObject)) return true;

            // because we delete the animators during the build
            if (binding.type.IsAssignableFrom(typeof(Animator))) {
                if (binding.path == "") return true;
                if (animators != null && animators.GetSubControllers().Any(s => s.owner == obj)) return true;
            }

            if (!typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) {
                // This can happen if the component type they were animating is no longer available, such as
                // if the script no longer exists in the project.
                return false;
            }
            if (obj.GetComponent(binding.type) != null) return true;
            if (binding.type == typeof(BoxCollider) && obj.GetComponent<VRCStation>() != null) return true;
#if VRCSDK_HAS_VRCCONSTRAINTS
            // Due to "half-upgraded" assets, animations may point to the wrong kind of constraint
            // This will be fixed later in the build in UpgradeToVrcConstraintsService
            if (typeof(IConstraint).IsAssignableFrom(binding.type) && obj.GetComponent<IVRCConstraint>() != null) return true;
            if (typeof(IVRCConstraint).IsAssignableFrom(binding.type) && obj.GetComponent<IConstraint>() != null) return true;
#endif
            return false;
        }
    }
}
