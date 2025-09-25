using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Feature {
    [FeatureTitle("CS Retarget")]
    internal class ConstraintRetargetBuilder : FeatureBuilder<ConstraintRetarget> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly Func<VFGameObject> componentObject;

        [FeatureBuilderAction]
        public void Apply() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            var constraints = componentObject().GetComponents<VRCConstraintBase>();
            if (!constraints.Any()) {
                throw new Exception("Object does not contain a VRC Constraint");
            }

            VFGameObject newTarget;
            if (model.bone == HumanBodyBones.LastBone) {
                newTarget = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Hips)?.parent;
            } else {
                newTarget = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, model.bone);
            }

            foreach (var c in constraints) {
                if (newTarget == null) Object.DestroyImmediate(c);
                else c.TargetTransform = newTarget;
            }
#else
            throw new Exception("This version of the VRCSDK does not support VRC constraints");
#endif
        }

        [FeatureEditor]
        public static VisualElement Builder(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This component forces a VRC Constraint on the current object to point to affect a bone on the base avatar." +
                "\n\n" +
                "This is basically only useful for GogoLoco."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bone"), "Bone to target"));
            return content;
        }
    }
}
