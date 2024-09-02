using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Inspector;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace VF.Utils {
    internal static class ConstraintUtils {
        public static void MakeWorldSpace(VFGameObject obj) {
            obj.worldScale = Vector3.one;
            
#if VRCSDK_HAS_VRCCONSTRAINTS
            var constraint = obj.AddComponent<VRCScaleConstraint>();
            constraint.IsActive = true;
            constraint.Locked = true;
            constraint.FreezeToWorld = true;
#else
            var p = obj.AddComponent<ScaleConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;
#endif
        }
    }
}
