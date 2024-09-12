using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("Cross Eye Fix")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class CrossEyeFixBuilder : FeatureBuilder<CrossEyeFix2> {
        [VFAutowired] private readonly ObjectMoveService mover;
        
        [FeatureBuilderAction]
        public void Apply() {
            if (!BuildTargetUtils.IsDesktop()) {
                return;
            }

            var avatar = manager.Avatar;
            if (!avatar.enableEyeLook) return;
            var eyeLeft = avatar.customEyeLookSettings.leftEye;
            var eyeRight = avatar.customEyeLookSettings.rightEye;
            if (eyeLeft) avatar.customEyeLookSettings.leftEye = AddFakeEye(eyeLeft);
            if (eyeRight) avatar.customEyeLookSettings.rightEye = AddFakeEye(eyeRight);
        }

        private VFGameObject AddFakeEye(VFGameObject originalEye) {
            var baseName = originalEye.name;

            var realEyeUp = GameObjects.Create(baseName + ".Up", originalEye.parent, useTransformFrom: originalEye);
            realEyeUp.worldRotation = Quaternion.identity;
            
            var fakeEye = GameObjects.Create(baseName + ".Fake", originalEye.parent, useTransformFrom: originalEye);

            var fakeEyeUp = GameObjects.Create(baseName + ".Fake.Up", fakeEye, useTransformFrom: realEyeUp);

            mover.Move(originalEye.gameObject, realEyeUp.gameObject);

            var constraint = realEyeUp.AddComponent<RotationConstraint>();
            constraint.AddSource(new ConstraintSource() {
                sourceTransform = fakeEyeUp,
                weight = 1
            });
            constraint.rotationAxis = Axis.X | Axis.Y;
            constraint.weight = 1;
            constraint.constraintActive = true;
            constraint.locked = true;

            return fakeEye;
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This feature automatically tweaks your avatar eyes so that they can't go cross-eyed in VRChat." +
                " It does this by eliminating eye bone roll through fake eye bones and rotation constraints."
            );
        }
    }
}